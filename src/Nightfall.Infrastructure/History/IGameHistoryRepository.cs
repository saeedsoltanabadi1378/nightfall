using Nightfall.Domain;

namespace Nightfall.Infrastructure.History;

public interface IGameHistoryRepository
{
    Task SaveCompletedGameAsync(GameState game, long telegramChatId, CancellationToken ct = default);
    Task<GameRecord?> GetAsync(Guid gameId, CancellationToken ct = default);
    Task<IReadOnlyList<GameRecord>> GetForChatAsync(long telegramChatId, CancellationToken ct = default);
}
