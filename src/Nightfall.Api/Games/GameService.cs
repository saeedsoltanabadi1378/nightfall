using Nightfall.Domain;
using Nightfall.Infrastructure;
using Nightfall.Infrastructure.History;
using Nightfall.Infrastructure.Sessions;

namespace Nightfall.Api.Games;

public sealed class GameService
{
    private readonly IGameSessionStore _sessionStore;
    private readonly IGameHistoryRepository _historyRepository;
    private readonly IGameNotifier _notifier;

    public GameService(IGameSessionStore sessionStore, IGameHistoryRepository historyRepository, IGameNotifier notifier)
    {
        _sessionStore = sessionStore;
        _historyRepository = historyRepository;
        _notifier = notifier;
    }

    public async Task<Guid> CreateGameAsync(long telegramChatId, long creatorTelegramUserId, string creatorUsername)
    {
        var game = new GameState(telegramChatId: telegramChatId);
        var creatorId = PlayerIdentity.DeriveId(game.GameId, creatorTelegramUserId);
        game.AddPlayer(new Player(creatorId, creatorUsername));

        await _sessionStore.SaveAsync(game);
        return game.GameId;
    }

    public async Task<GameState> LoadOrThrowAsync(Guid gameId) =>
        await _sessionStore.GetAsync(gameId) ?? throw new GameNotFoundException(gameId);

    public async Task JoinGameAsync(Guid gameId, long telegramUserId, string username)
    {
        var game = await LoadOrThrowAsync(gameId);
        var playerId = PlayerIdentity.DeriveId(gameId, telegramUserId);

        if (game.GetPlayer(playerId) is null)
        {
            game.AddPlayer(new Player(playerId, username));
            await SaveAndNotifyAsync(game);
        }
    }

    public async Task StartGameAsync(Guid gameId)
    {
        var game = await LoadOrThrowAsync(gameId);
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

    public async Task<NightResult> ResolveNightAsync(Guid gameId)
    {
        var game = await LoadOrThrowAsync(gameId);
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

    public async Task<VotingResult> ResolveVotingAsync(Guid gameId)
    {
        var game = await LoadOrThrowAsync(gameId);
        var result = game.ResolveVoting();
        await SaveAndNotifyAsync(game);
        return result;
    }

    public async Task StartVotingAsync(Guid gameId)
    {
        var game = await LoadOrThrowAsync(gameId);
        game.StartVoting();
        await SaveAndNotifyAsync(game);
    }

    public async Task StartNightAsync(Guid gameId)
    {
        var game = await LoadOrThrowAsync(gameId);
        game.StartNight();
        await SaveAndNotifyAsync(game);
    }

    private async Task SaveAndNotifyAsync(GameState game)
    {
        await _sessionStore.SaveAsync(game);

        if (game.CurrentPhase == GamePhase.Ended)
        {
            await _historyRepository.SaveCompletedGameAsync(game);
        }

        await _notifier.NotifyGameUpdatedAsync(game);
    }
}
