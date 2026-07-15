using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nightfall.Domain;
using Nightfall.Infrastructure.Auth;
using Nightfall.Infrastructure.Sessions;
using Telegram.Bot.Types;

namespace Nightfall.Bot;

public sealed class CommandDispatcher
{
    private readonly INightfallApiClient _api;
    private readonly IBotMessenger _messenger;
    private readonly IChatGameIndex _chatGameIndex;
    private readonly IGameRosterStore _rosterStore;
    private readonly BotOptions _botOptions;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(
        INightfallApiClient api,
        IBotMessenger messenger,
        IChatGameIndex chatGameIndex,
        IGameRosterStore rosterStore,
        IOptions<BotOptions> botOptions,
        ILogger<CommandDispatcher> logger)
    {
        _api = api;
        _messenger = messenger;
        _chatGameIndex = chatGameIndex;
        _rosterStore = rosterStore;
        _botOptions = botOptions.Value;
        _logger = logger;
    }

    public async Task HandleMessageAsync(Message message)
    {
        if (message.Text is not { Length: > 0 } text || message.From is null)
            return;

        string? command = ParseCommand(text);
        if (command is null)
            return;

        long chatId = message.Chat.Id;
        var actor = ToIdentity(message.From);

        try
        {
            switch (command)
            {
                case "newgame": await HandleNewGameAsync(chatId, actor); break;
                case "join": await HandleJoinAsync(chatId, actor); break;
                case "startgame": await HandleStartGameAsync(chatId, actor); break;
                case "resolvenight": await HandleResolveNightAsync(chatId, actor); break;
                case "startvoting": await HandleStartVotingAsync(chatId, actor); break;
                case "resolvevoting": await HandleResolveVotingAsync(chatId, actor); break;
                case "startnight": await HandleStartNightAsync(chatId, actor); break;
                case "myrole": await HandleMyRoleAsync(chatId, actor); break;
                case "help": case "start": await HandleHelpAsync(chatId); break;
            }
        }
        catch (NightfallApiException ex)
        {
            await _messenger.SendTextAsync(chatId, $"⚠️ {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing command {Command} in chat {ChatId}", command, chatId);
            await _messenger.SendTextAsync(chatId, "⚠️ Something went wrong handling that command.");
        }
    }

    /// <summary>Extracts the command word from a message like "/newgame", "/newgame@BotName", or
    /// "/newgame extra args" -> "newgame". Returns null for non-command text.</summary>
    internal static string? ParseCommand(string text)
    {
        if (!text.StartsWith('/'))
            return null;

        int end = text.IndexOfAny(new[] { ' ', '\n', '\t' });
        string firstToken = end < 0 ? text : text[..end];

        int at = firstToken.IndexOf('@');
        string command = at >= 0 ? firstToken[1..at] : firstToken[1..];

        return command.Length == 0 ? null : command.ToLowerInvariant();
    }

    internal static TelegramIdentity ToIdentity(User user) =>
        new(user.Id, user.FirstName, user.LastName, user.Username, user.LanguageCode);

    internal static string DisplayName(TelegramIdentity identity) =>
        identity.Username is { Length: > 0 } username ? $"@{username}" : identity.FirstName;

    private async Task<Guid?> GetActiveGameOrReplyAsync(long chatId)
    {
        var gameId = await _chatGameIndex.GetActiveGameAsync(chatId);
        if (gameId is null)
        {
            await _messenger.SendTextAsync(chatId, "No active game in this chat. Use /newgame to start one.");
        }
        return gameId;
    }

    private async Task HandleNewGameAsync(long chatId, TelegramIdentity actor)
    {
        var existing = await _chatGameIndex.GetActiveGameAsync(chatId);
        if (existing is not null)
        {
            await _messenger.SendTextAsync(chatId, "A game is already in progress in this chat.");
            return;
        }

        await _api.CreateGameAsync(chatId, actor);
        await _messenger.SendTextAsync(chatId,
            $"🌙 New Nightfall game created by {DisplayName(actor)}! Others can /join. Once everyone's in, /startgame.");
    }

    private async Task HandleJoinAsync(long chatId, TelegramIdentity actor)
    {
        var gameId = await GetActiveGameOrReplyAsync(chatId);
        if (gameId is null)
            return;

        await _api.JoinGameAsync(gameId.Value, actor);
        await _messenger.SendTextAsync(chatId, $"{DisplayName(actor)} joined the game.");
    }

    private async Task HandleStartGameAsync(long chatId, TelegramIdentity actor)
    {
        var gameId = await GetActiveGameOrReplyAsync(chatId);
        if (gameId is null)
            return;

        await _api.StartGameAsync(gameId.Value, actor);
        await _messenger.SendTextAsync(chatId, "🎭 Roles have been assigned! Check your DMs for your role.");

        await RevealRolesAsync(chatId, gameId.Value);
    }

    private async Task RevealRolesAsync(long chatId, Guid gameId)
    {
        var roster = await _rosterStore.GetAsync(gameId);
        var couldNotReach = new List<string>();
        string? miniAppUrl = _botOptions.MiniAppBaseUrl is { Length: > 0 } baseUrl ? $"{baseUrl}?startapp={gameId}" : null;

        foreach (var entry in roster)
        {
            try
            {
                var playerIdentity = new TelegramIdentity(entry.TelegramUserId, entry.Username, null, entry.Username, null);
                var view = await _api.GetGameAsync(gameId, playerIdentity);
                await _messenger.SendWithMiniAppButtonAsync(entry.TelegramUserId, FormatRoleReveal(view), miniAppUrl);
            }
            catch (Exception ex)
            {
                // Most commonly: Telegram refuses to let a bot DM a user who hasn't started a
                // conversation with it yet. Not fatal to the game — just tell them in the group.
                _logger.LogWarning(ex, "Could not DM role reveal to {TelegramUserId}", entry.TelegramUserId);
                couldNotReach.Add(entry.Username);
            }
        }

        if (couldNotReach.Count > 0)
        {
            string names = string.Join(", ", couldNotReach.Select(u => $"@{u}"));
            await _messenger.SendTextAsync(chatId, $"{names}: please message me privately first so I can DM you your role.");
        }
    }

    private static string FormatRoleReveal(GameViewDto view) => view.YourRole switch
    {
        Role.Villager => "👤 You are a *Villager*. Work with the town to find the Mafia.",
        Role.Detective => "🔍 You are the *Detective*. Each night, investigate one player to learn if they're Mafia-aligned.",
        Role.Doctor => "💉 You are the *Doctor*. Each night, choose one player to protect from the Mafia's kill.",
        Role.Mafia => "🔪 You are *Mafia*. Work with your team to eliminate the Villagers without being caught.",
        Role.Godfather => "👑 You are the *Godfather*, leader of the Mafia. Choose who dies each night.",
        _ => "Your role hasn't been assigned yet."
    } + "\n\nOpen the Mini App to play.";

    private async Task HandleResolveNightAsync(long chatId, TelegramIdentity actor)
    {
        var gameId = await GetActiveGameOrReplyAsync(chatId);
        if (gameId is null)
            return;

        var result = await _api.ResolveNightAsync(gameId.Value, actor);
        var view = await _api.GetGameAsync(gameId.Value, actor);

        await _messenger.SendTextAsync(chatId, FormatNightResult(result, view));
        await AnnounceIfGameEndedAsync(chatId, view);
    }

    private static string FormatNightResult(NightResult result, GameViewDto view)
    {
        if (!result.Eliminated.HasValue)
        {
            return result.TargetWasSaved
                ? "🌅 The Doctor saved the target! Nobody died last night."
                : "🌅 Nobody was targeted last night.";
        }

        var victim = view.Players.FirstOrDefault(p => p.PlayerId == result.Eliminated.Value);
        string name = victim?.TelegramUsername ?? "Someone";
        string roleText = victim?.RevealedRole is { } role ? $" They were a {role}." : "";
        return $"🌅 {name} was found dead this morning.{roleText}";
    }

    private async Task HandleStartVotingAsync(long chatId, TelegramIdentity actor)
    {
        var gameId = await GetActiveGameOrReplyAsync(chatId);
        if (gameId is null)
            return;

        await _api.StartVotingAsync(gameId.Value, actor);
        await _messenger.SendTextAsync(chatId, "🗳️ Voting is open. Cast your votes, then /resolvevoting.");
    }

    private async Task HandleResolveVotingAsync(long chatId, TelegramIdentity actor)
    {
        var gameId = await GetActiveGameOrReplyAsync(chatId);
        if (gameId is null)
            return;

        var result = await _api.ResolveVotingAsync(gameId.Value, actor);
        var view = await _api.GetGameAsync(gameId.Value, actor);

        await _messenger.SendTextAsync(chatId, FormatVotingResult(result, view));
        await AnnounceIfGameEndedAsync(chatId, view);
    }

    private static string FormatVotingResult(VotingResult result, GameViewDto view)
    {
        if (result.WasTie)
            return "🤝 The vote was tied. Nobody was eliminated.";

        if (!result.Eliminated.HasValue)
            return "🗳️ No votes were cast. Nobody was eliminated.";

        var eliminated = view.Players.FirstOrDefault(p => p.PlayerId == result.Eliminated.Value);
        string name = eliminated?.TelegramUsername ?? "Someone";
        string roleText = eliminated?.RevealedRole is { } role ? $" They were a {role}." : "";
        return $"⚖️ The town has voted out {name}.{roleText}";
    }

    private async Task HandleStartNightAsync(long chatId, TelegramIdentity actor)
    {
        var gameId = await GetActiveGameOrReplyAsync(chatId);
        if (gameId is null)
            return;

        await _api.StartNightAsync(gameId.Value, actor);
        await _messenger.SendTextAsync(chatId, "🌙 Night falls. Check your DMs / the Mini App for your night action.");
    }

    private async Task AnnounceIfGameEndedAsync(long chatId, GameViewDto view)
    {
        if (view.WinCondition == WinCondition.None)
            return;

        string winner = view.WinCondition == WinCondition.VillagersWin ? "The Villagers" : "The Mafia";
        await _messenger.SendTextAsync(chatId, $"🏆 Game over! {winner} win!");
    }

    private async Task HandleMyRoleAsync(long chatId, TelegramIdentity actor)
    {
        var gameId = await GetActiveGameOrReplyAsync(chatId);
        if (gameId is null)
            return;

        var view = await _api.GetGameAsync(gameId.Value, actor);
        try
        {
            await _messenger.SendTextAsync(actor.Id, FormatRoleReveal(view));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not DM /myrole to {TelegramUserId}", actor.Id);
            await _messenger.SendTextAsync(chatId, $"{DisplayName(actor)}: please message me privately first so I can DM you.");
        }
    }

    private Task HandleHelpAsync(long chatId) => _messenger.SendTextAsync(chatId,
        "Nightfall commands:\n" +
        "/newgame — start a new lobby in this chat\n" +
        "/join — join the current lobby\n" +
        "/startgame — assign roles and begin\n" +
        "/resolvenight — resolve the current night\n" +
        "/startvoting — open voting\n" +
        "/resolvevoting — tally votes\n" +
        "/startnight — begin the next night\n" +
        "/myrole — DM yourself your current role");
}
