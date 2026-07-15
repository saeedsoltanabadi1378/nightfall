using Nightfall.Domain;

namespace Nightfall.Infrastructure.Sessions;

/// <summary>Fast read/write store for a live (in-progress) game's full state, keyed by GameId.</summary>
public interface IGameSessionStore
{
    Task<GameState?> GetAsync(Guid gameId);
    Task SaveAsync(GameState game);
    Task RemoveAsync(Guid gameId);
    Task<IReadOnlyList<Guid>> ListActiveIdsAsync();
}
