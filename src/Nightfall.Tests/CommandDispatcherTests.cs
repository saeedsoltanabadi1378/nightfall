using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nightfall.Bot;
using Nightfall.Domain;
using Nightfall.Infrastructure.Auth;
using Nightfall.Infrastructure.Sessions;
using Telegram.Bot.Types;

namespace Nightfall.Tests;

public class CommandDispatcherTests
{
    private const long ChatId = 1001;

    private static (CommandDispatcher Dispatcher, FakeBotMessenger Messenger, FakeNightfallApiClient Api, IChatGameIndex ChatIndex, IGameRosterStore Roster)
        CreateDispatcher(BotOptions? options = null)
    {
        var cache = new InMemoryKeyValueCache();
        var chatIndex = new ChatGameIndex(cache);
        var roster = new GameRosterStore(cache);
        var messenger = new FakeBotMessenger();
        var api = new FakeNightfallApiClient();

        var dispatcher = new CommandDispatcher(
            api, messenger, chatIndex, roster,
            Options.Create(options ?? new BotOptions { NightfallApiBaseUrl = "http://localhost" }),
            NullLogger<CommandDispatcher>.Instance);

        return (dispatcher, messenger, api, chatIndex, roster);
    }

    private static Message TextMessage(long chatId, long userId, string username, string text) => new()
    {
        Text = text,
        Chat = new Chat { Id = chatId },
        From = new User { Id = userId, FirstName = username, Username = username }
    };

    [Fact]
    public async Task NewGame_NoExistingGame_CreatesGameAndAnnounces()
    {
        var (dispatcher, messenger, api, _, _) = CreateDispatcher();

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/newgame"));

        Assert.Contains("CreateGame(1001)", api.Calls);
        Assert.Single(messenger.Sent);
        Assert.Contains("New Nightfall game created", messenger.Sent[0].Text);
    }

    [Fact]
    public async Task NewGame_GameAlreadyActiveInChat_RefusesWithoutCallingApi()
    {
        var (dispatcher, messenger, api, chatIndex, _) = CreateDispatcher();
        await chatIndex.SetActiveGameAsync(ChatId, Guid.NewGuid());

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/newgame"));

        Assert.DoesNotContain(api.Calls, c => c.StartsWith("CreateGame"));
        Assert.Contains("already in progress", messenger.Sent[0].Text);
    }

    [Fact]
    public async Task Join_NoActiveGame_RepliesWithoutCallingApi()
    {
        var (dispatcher, messenger, api, _, _) = CreateDispatcher();

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 2, "bob", "/join"));

        Assert.DoesNotContain(api.Calls, c => c.StartsWith("Join"));
        Assert.Contains("No active game", messenger.Sent[0].Text);
    }

    [Fact]
    public async Task Join_ActiveGame_CallsApiAndAnnouncesJoin()
    {
        var (dispatcher, messenger, api, chatIndex, _) = CreateDispatcher();
        await chatIndex.SetActiveGameAsync(ChatId, api.CreatedGameId);

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 2, "bob", "/join"));

        Assert.Contains("Join(2)", api.Calls);
        Assert.Contains("@bob joined the game", messenger.Sent[0].Text);
    }

    [Fact]
    public async Task SoloTest_DisabledByDefault_DoesNotCallApi()
    {
        var (dispatcher, messenger, api, _, _) = CreateDispatcher();

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/solotest"));

        Assert.Empty(api.Calls);
        Assert.Contains("disabled", messenger.Sent.Single().Text);
    }

    [Fact]
    public async Task SoloTest_WithTwoRealPlayers_AddsThreeSyntheticPlayersAndStarts()
    {
        var options = new BotOptions
        {
            NightfallApiBaseUrl = "http://localhost",
            MiniAppBaseUrl = "https://t.me/NightfallBot/game",
            SoloTestEnabled = true
        };
        var (dispatcher, messenger, api, chatIndex, _) = CreateDispatcher(options);
        await chatIndex.SetActiveGameAsync(ChatId, api.CreatedGameId);
        var aliceId = Guid.NewGuid();
        api.ViewsByTelegramUserId[1] = new GameViewDto(
            api.CreatedGameId, GamePhase.Lobby, 0,
            new[]
            {
                new PlayerViewDto(aliceId, "alice", true, null),
                new PlayerViewDto(Guid.NewGuid(), "bob", true, null)
            },
            aliceId, Role.Villager, true, null, null, null, WinCondition.None);

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/solotest"));

        Assert.Equal(3, api.Calls.Count(call => call.StartsWith("Join(")));
        Assert.Contains("StartGame", api.Calls);
        Assert.Contains(messenger.Sent, sent => sent.ChatId == 1 && sent.MiniAppUrl != null);
    }

    [Fact]
    public async Task StartGame_RevealsRolesByDm_AndAnnouncesInGroup()
    {
        var (dispatcher, messenger, api, chatIndex, roster) = CreateDispatcher();
        await chatIndex.SetActiveGameAsync(ChatId, api.CreatedGameId);
        await roster.AddAsync(api.CreatedGameId, 10, "detective_dan");
        await roster.AddAsync(api.CreatedGameId, 20, "villager_vic");

        api.ViewsByTelegramUserId[10] = new GameViewDto(
            api.CreatedGameId, GamePhase.NightZero, 0, Array.Empty<PlayerViewDto>(),
            Guid.NewGuid(), Role.Detective, true, null, null, null, WinCondition.None);
        api.ViewsByTelegramUserId[20] = new GameViewDto(
            api.CreatedGameId, GamePhase.NightZero, 0, Array.Empty<PlayerViewDto>(),
            Guid.NewGuid(), Role.Villager, true, null, null, null, WinCondition.None);

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/startgame"));

        Assert.Contains("StartGame", api.Calls);
        var dmToDetective = messenger.Sent.Single(m => m.ChatId == 10);
        Assert.Contains("Detective", dmToDetective.Text);
        var dmToVillager = messenger.Sent.Single(m => m.ChatId == 20);
        Assert.Contains("Villager", dmToVillager.Text);
        // Group announcement that roles were assigned.
        Assert.Contains(messenger.Sent, m => m.ChatId == ChatId && m.Text.Contains("Roles have been assigned"));
    }

    [Fact]
    public async Task StartGame_WithMiniAppUrlConfigured_IncludesStartParamInDm()
    {
        var (dispatcher, messenger, api, chatIndex, roster) = CreateDispatcher(
            new BotOptions { NightfallApiBaseUrl = "http://localhost", MiniAppBaseUrl = "https://t.me/NightfallBot/app" });
        await chatIndex.SetActiveGameAsync(ChatId, api.CreatedGameId);
        await roster.AddAsync(api.CreatedGameId, 10, "dan");
        api.ViewsByTelegramUserId[10] = new GameViewDto(
            api.CreatedGameId, GamePhase.NightZero, 0, Array.Empty<PlayerViewDto>(),
            Guid.NewGuid(), Role.Villager, true, null, null, null, WinCondition.None);

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/startgame"));

        var dm = messenger.Sent.Single(m => m.ChatId == 10);
        Assert.Equal($"https://t.me/NightfallBot/app?startapp={api.CreatedGameId}", dm.MiniAppUrl);
        var groupAnnouncement = messenger.Sent.Single(m => m.ChatId == ChatId);
        Assert.Equal($"https://t.me/NightfallBot/app?startapp={api.CreatedGameId}", groupAnnouncement.Url);
    }

    [Fact]
    public async Task StartGame_PlayerUnreachableForDm_FallsBackToGroupNotice()
    {
        var (dispatcher, messenger, api, chatIndex, roster) = CreateDispatcher();
        await chatIndex.SetActiveGameAsync(ChatId, api.CreatedGameId);
        await roster.AddAsync(api.CreatedGameId, 10, "shy_sam");
        api.ViewsByTelegramUserId[10] = new GameViewDto(
            api.CreatedGameId, GamePhase.NightZero, 0, Array.Empty<PlayerViewDto>(),
            Guid.NewGuid(), Role.Villager, true, null, null, null, WinCondition.None);
        messenger.UnreachableChatIds.Add(10);

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/startgame"));

        Assert.Contains(messenger.Sent, m => m.ChatId == ChatId && m.Text.Contains("@shy_sam") && m.Text.Contains("message me privately"));
    }

    [Fact]
    public async Task ResolveNight_PlayerEliminated_AnnouncesNameAndRole()
    {
        var (dispatcher, messenger, api, chatIndex, _) = CreateDispatcher();
        await chatIndex.SetActiveGameAsync(ChatId, api.CreatedGameId);
        var victimId = Guid.NewGuid();
        api.NightResultToReturn = new NightResult(1, victimId, false, null, null, null);
        api.ViewsByTelegramUserId[1] = new GameViewDto(
            api.CreatedGameId, GamePhase.Day, 1,
            new[] { new PlayerViewDto(victimId, "unlucky_ursula", false, Role.Villager) },
            Guid.NewGuid(), Role.Detective, true, null, null, null, WinCondition.None);

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/resolvenight"));

        Assert.Contains(messenger.Sent, m => m.Text.Contains("unlucky_ursula") && m.Text.Contains("Villager"));
    }

    [Fact]
    public async Task ResolveNight_TargetSaved_AnnouncesSave()
    {
        var (dispatcher, messenger, api, chatIndex, _) = CreateDispatcher();
        await chatIndex.SetActiveGameAsync(ChatId, api.CreatedGameId);
        api.NightResultToReturn = new NightResult(1, null, true, null, null, null);
        api.ViewsByTelegramUserId[1] = new GameViewDto(
            api.CreatedGameId, GamePhase.Day, 1, Array.Empty<PlayerViewDto>(),
            Guid.NewGuid(), Role.Doctor, true, null, null, null, WinCondition.None);

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/resolvenight"));

        Assert.Contains(messenger.Sent, m => m.Text.Contains("Doctor saved the target"));
    }

    [Fact]
    public async Task ResolveVoting_Tie_AnnouncesTie()
    {
        var (dispatcher, messenger, api, chatIndex, _) = CreateDispatcher();
        await chatIndex.SetActiveGameAsync(ChatId, api.CreatedGameId);
        api.VotingResultToReturn = new VotingResult(null, true, new[] { Guid.NewGuid(), Guid.NewGuid() }, null);
        api.ViewsByTelegramUserId[1] = new GameViewDto(
            api.CreatedGameId, GamePhase.Results, 1, Array.Empty<PlayerViewDto>(),
            Guid.NewGuid(), Role.Villager, true, null, null, null, WinCondition.None);

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/resolvevoting"));

        Assert.Contains(messenger.Sent, m => m.Text.Contains("tied"));
    }

    [Fact]
    public async Task ResolveVoting_GameEnds_AnnouncesWinner()
    {
        var (dispatcher, messenger, api, chatIndex, _) = CreateDispatcher();
        await chatIndex.SetActiveGameAsync(ChatId, api.CreatedGameId);
        var eliminatedId = Guid.NewGuid();
        api.VotingResultToReturn = new VotingResult(eliminatedId, false, new[] { eliminatedId }, null);
        api.ViewsByTelegramUserId[1] = new GameViewDto(
            api.CreatedGameId, GamePhase.Ended, 1,
            new[] { new PlayerViewDto(eliminatedId, "last_godfather", false, Role.Godfather) },
            Guid.NewGuid(), Role.Villager, true, null, null, null, WinCondition.VillagersWin);

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/resolvevoting"));

        Assert.Contains(messenger.Sent, m => m.Text.Contains("last_godfather"));
        Assert.Contains(messenger.Sent, m => m.Text.Contains("Villagers win"));
    }

    [Fact]
    public async Task ApiThrowsNightfallApiException_RepliesWithFriendlyMessage()
    {
        var (dispatcher, messenger, api, chatIndex, _) = CreateDispatcher();
        await chatIndex.SetActiveGameAsync(ChatId, api.CreatedGameId);
        api.ThrowOnNextCall = new NightfallApiException("It is not your turn to act.");

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/resolvenight"));

        Assert.Contains(messenger.Sent, m => m.Text.Contains("It is not your turn to act."));
    }

    [Fact]
    public async Task NonCommandText_IsIgnored()
    {
        var (dispatcher, messenger, _, _, _) = CreateDispatcher();

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "hello everyone"));

        Assert.Empty(messenger.Sent);
    }

    [Fact]
    public async Task UnknownCommand_IsIgnored()
    {
        var (dispatcher, messenger, _, _, _) = CreateDispatcher();

        await dispatcher.HandleMessageAsync(TextMessage(ChatId, 1, "alice", "/notarealcommand"));

        Assert.Empty(messenger.Sent);
    }
}
