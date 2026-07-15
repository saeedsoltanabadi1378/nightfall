namespace Nightfall.Infrastructure.Sessions;

public interface IGameRosterStore
{
    Task AddAsync(Guid gameId, long telegramUserId, string username);
    Task<IReadOnlyList<GameRosterEntry>> GetAsync(Guid gameId);
    Task RemoveAsync(Guid gameId);
}
