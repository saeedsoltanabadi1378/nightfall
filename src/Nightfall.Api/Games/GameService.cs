using Nightfall.Domain;
using Nightfall.Infrastructure;
using Nightfall.Infrastructure.History;
using Nightfall.Infrastructure.Admin;
using Microsoft.EntityFrameworkCore;
using Nightfall.Infrastructure.Sessions;

namespace Nightfall.Api.Games;

public sealed class GameService
{
    private readonly IGameSessionStore _sessionStore;
    private readonly IGameHistoryRepository _historyRepository;
    private readonly IGameNotifier _notifier;
    private readonly IChatGameIndex _chatGameIndex;
    private readonly IGameRosterStore _rosterStore;
    private readonly IBotSettingsService _settings;
    private readonly NightfallDbContext _db;
    private readonly GameMutationLock _mutationLock;

    public GameService(
        IGameSessionStore sessionStore,
        IGameHistoryRepository historyRepository,
        IGameNotifier notifier,
        IChatGameIndex chatGameIndex,
        IGameRosterStore rosterStore, IBotSettingsService settings, NightfallDbContext db, GameMutationLock mutationLock)
    {
        _sessionStore = sessionStore;
        _historyRepository = historyRepository;
        _notifier = notifier;
        _chatGameIndex = chatGameIndex;
        _rosterStore = rosterStore;
        _settings = settings;
        _db = db;
        _mutationLock = mutationLock;
    }

    public async Task<Guid> CreateGameAsync(long telegramChatId, long creatorTelegramUserId, string creatorUsername)
    {
        var settings = await _settings.GetAsync();
        var game = new GameState(new GameConfig { MinPlayers = settings.MinPlayers, MaxPlayers = settings.MaxPlayers }, telegramChatId: telegramChatId);
        var creatorId = PlayerIdentity.DeriveId(game.GameId, creatorTelegramUserId);
        game.AddPlayer(new Player(creatorId, creatorUsername));

        await _sessionStore.SaveAsync(game);
        await _chatGameIndex.SetActiveGameAsync(telegramChatId, game.GameId);
        await _rosterStore.AddAsync(game.GameId, creatorTelegramUserId, creatorUsername);
        await TrackActivityAsync(telegramChatId, creatorTelegramUserId, creatorUsername);
        _db.OperationalEvents.Add(new() { Category = "Game", Message = "Game created", TargetType = "Game", TargetId = game.GameId.ToString() });
        await _db.SaveChangesAsync();
        return game.GameId;
    }

    public async Task<GameState> LoadOrThrowAsync(Guid gameId) =>
        await _sessionStore.GetAsync(gameId) ?? throw new GameNotFoundException(gameId);

    public async Task<Guid> GetActiveGameForChatOrThrowAsync(long telegramChatId) =>
        await _chatGameIndex.GetActiveGameAsync(telegramChatId)
            ?? throw GameNotFoundException.ForChat(telegramChatId);

    public async Task JoinGameAsync(Guid gameId, long telegramUserId, string username)
    {
        var game = await LoadOrThrowAsync(gameId);
        var playerId = PlayerIdentity.DeriveId(gameId, telegramUserId);

        if (game.GetPlayer(playerId) is null)
        {
            game.AddPlayer(new Player(playerId, username));
            await SaveAndNotifyAsync(game);
            await _rosterStore.AddAsync(gameId, telegramUserId, username);
            if (game.TelegramChatId is { } chatId) { await TrackActivityAsync(chatId, telegramUserId, username); await _db.SaveChangesAsync(); }
        }
    }

    public async Task StartGameAsync(Guid gameId, long telegramUserId)
    {
        var game = await LoadOrThrowAsync(gameId);
        EnsureController(game, telegramUserId);
        game.AssignRoles();
        await SaveAndNotifyAsync(game);
    }

    public async Task SubmitNightActionAsync(Guid gameId, long telegramUserId, Guid targetPlayerId, NightActionType actionType)
    {
        var game = await LoadOrThrowAsync(gameId);
        var actorId = PlayerIdentity.DeriveId(gameId, telegramUserId);
        game.SubmitNightAction(actorId, targetPlayerId, actionType);
        await SaveAndNotifyAsync(game);
    }

    public async Task<NightResult> ResolveNightAsync(Guid gameId, long telegramUserId)
    {
        using var held = await _mutationLock.EnterAsync(gameId);
        var game = await LoadOrThrowAsync(gameId);
        EnsureController(game, telegramUserId);
        if (game.CurrentPhase == GamePhase.Night && !game.AreRequiredNightActionsComplete())
            throw new GameException("All living night roles must submit their actions before night can end.");
        var result = game.ResolveNight();
        await SaveAndNotifyAsync(game);
        return result;
    }

    public async Task SubmitVoteAsync(Guid gameId, long telegramUserId, Guid? targetPlayerId)
    {
        var game = await LoadOrThrowAsync(gameId);
        var voterId = PlayerIdentity.DeriveId(gameId, telegramUserId);
        game.SubmitVote(voterId, targetPlayerId);
        await SaveAndNotifyAsync(game);
    }

    public async Task<VotingResult> ResolveVotingAsync(Guid gameId, long telegramUserId)
    {
        var game = await LoadOrThrowAsync(gameId);
        EnsureController(game, telegramUserId);
        var result = game.ResolveVoting();
        await SaveAndNotifyAsync(game);
        return result;
    }

    public async Task StartVotingAsync(Guid gameId, long telegramUserId)
    {
        using var held = await _mutationLock.EnterAsync(gameId);
        var game = await LoadOrThrowAsync(gameId);
        EnsureController(game, telegramUserId);
        game.StartVoting();
        await SaveAndNotifyAsync(game);
    }

    public async Task RequestChallengeAsync(Guid gameId, long telegramUserId)
    {
        using var held = await _mutationLock.EnterAsync(gameId);
        var game = await LoadOrThrowAsync(gameId);
        game.RequestChallenge(PlayerIdentity.DeriveId(gameId, telegramUserId), DateTimeOffset.UtcNow);
        await SaveAndNotifyAsync(game);
    }

    public async Task CancelChallengeAsync(Guid gameId, long telegramUserId)
    {
        using var held = await _mutationLock.EnterAsync(gameId);
        var game = await LoadOrThrowAsync(gameId);
        game.CancelChallenge(PlayerIdentity.DeriveId(gameId, telegramUserId), DateTimeOffset.UtcNow);
        await SaveAndNotifyAsync(game);
    }

    public async Task AcceptChallengeAsync(Guid gameId, long telegramUserId, Guid challengerId)
    {
        using var held = await _mutationLock.EnterAsync(gameId);
        var game = await LoadOrThrowAsync(gameId);
        game.AcceptChallenge(PlayerIdentity.DeriveId(gameId, telegramUserId), challengerId, DateTimeOffset.UtcNow);
        await SaveAndNotifyAsync(game);
    }

    public async Task RejectChallengeAsync(Guid gameId, long telegramUserId, Guid challengerId)
    {
        using var held = await _mutationLock.EnterAsync(gameId);
        var game = await LoadOrThrowAsync(gameId);
        game.RejectChallenge(PlayerIdentity.DeriveId(gameId, telegramUserId), challengerId, DateTimeOffset.UtcNow);
        await SaveAndNotifyAsync(game);
    }

    public async Task FinishDiscussionAsync(Guid gameId, long telegramUserId)
    {
        using var held = await _mutationLock.EnterAsync(gameId);
        var game = await LoadOrThrowAsync(gameId);
        game.FinishDiscussionSegment(PlayerIdentity.DeriveId(gameId, telegramUserId), DateTimeOffset.UtcNow);
        await SaveAndNotifyAsync(game);
    }

    public async Task StartNightAsync(Guid gameId, long telegramUserId)
    {
        var game = await LoadOrThrowAsync(gameId);
        EnsureController(game, telegramUserId);
        game.StartNight();
        await SaveAndNotifyAsync(game);
    }

    private async Task SaveAndNotifyAsync(GameState game)
    {
        await _sessionStore.SaveAsync(game);

        if (game.CurrentPhase == GamePhase.Ended)
        {
            await _historyRepository.SaveCompletedGameAsync(game);
            if (game.TelegramChatId is { } chatId)
            {
                await _chatGameIndex.ClearActiveGameAsync(chatId);
            }
            await _rosterStore.RemoveAsync(game.GameId);
        }

        await _notifier.NotifyGameUpdatedAsync(game);
    }

    private static void EnsureController(GameState game, long telegramUserId)
    {
        var controller = game.Players.FirstOrDefault()
            ?? throw new ForbiddenGameActionException("This game has no controller.");
        var callerId = PlayerIdentity.DeriveId(game.GameId, telegramUserId);
        if (controller.Id != callerId)
            throw new ForbiddenGameActionException("Only the game creator can manage game phases.");
    }

    public async Task<bool> CancelByAdminAsync(Guid gameId, string reason, string actor)
    {
        var game = await _sessionStore.GetAsync(gameId);
        if (game is null) return await _db.Games.AnyAsync(x => x.Id == gameId && x.Status == "Cancelled");
        var roster = await _rosterStore.GetAsync(gameId);
        _db.Games.Add(new GameRecord { Id=game.GameId, TelegramChatId=game.TelegramChatId ?? 0, CreatedAt=game.CreatedAt.UtcDateTime, EndedAt=DateTime.UtcNow, Result=WinCondition.None, Status="Cancelled", CancellationReason=reason, Players=game.Players.Select(p => new GamePlayerRecord { Id=Guid.NewGuid(), PlayerId=p.Id, TelegramUserId=roster.FirstOrDefault(r=>r.Username==p.TelegramUsername)?.TelegramUserId, TelegramUsername=p.TelegramUsername, Role=p.Role, SurvivedToEnd=p.IsAlive, GodfatherRank=p.GodfatherRank }).ToList() });
        _db.OperationalEvents.Add(new() { Category="AdminAudit", Message="Game cancelled", IsAdminAudit=true, TargetType="Game", TargetId=gameId.ToString(), MetadataJson=System.Text.Json.JsonSerializer.Serialize(new { reason, actor }) });
        await _db.SaveChangesAsync();
        await _sessionStore.RemoveAsync(gameId); if(game.TelegramChatId is { } chat) await _chatGameIndex.ClearActiveGameAsync(chat); await _rosterStore.RemoveAsync(gameId); await _notifier.NotifyGameUpdatedAsync(game); return true;
    }

    private async Task TrackActivityAsync(long chatId, long userId, string username)
    {
        var now=DateTime.UtcNow; var user=await _db.UserProfiles.FindAsync(userId); if(user is null)_db.UserProfiles.Add(new(){TelegramUserId=userId,Username=username,FirstSeenAt=now,LastSeenAt=now}); else {user.Username=username;user.LastSeenAt=now;}
        var chat=await _db.ChatProfiles.FindAsync(chatId); if(chat is null)_db.ChatProfiles.Add(new(){TelegramChatId=chatId,FirstSeenAt=now,LastSeenAt=now}); else chat.LastSeenAt=now;
    }
}
