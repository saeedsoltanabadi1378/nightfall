using Nightfall.Domain;
using Nightfall.Infrastructure.Auth;

namespace Nightfall.Bot;

public interface INightfallApiClient
{
    Task<Guid> CreateGameAsync(long telegramChatId, TelegramIdentity actor);
    Task JoinGameAsync(Guid gameId, TelegramIdentity actor);
    Task StartGameAsync(Guid gameId, TelegramIdentity actor);
    Task<GameViewDto> GetGameAsync(Guid gameId, TelegramIdentity actor);
    Task<Guid?> GetActiveGameForChatAsync(long telegramChatId, TelegramIdentity actor);
    Task SubmitNightActionAsync(Guid gameId, TelegramIdentity actor, Guid targetPlayerId, NightActionType actionType);
    Task<NightResult> ResolveNightAsync(Guid gameId, TelegramIdentity actor);
    Task SubmitVoteAsync(Guid gameId, TelegramIdentity actor, Guid? targetPlayerId);
    Task<VotingResult> ResolveVotingAsync(Guid gameId, TelegramIdentity actor);
    Task StartVotingAsync(Guid gameId, TelegramIdentity actor);
    Task StartNightAsync(Guid gameId, TelegramIdentity actor);
}
