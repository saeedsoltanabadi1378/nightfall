using Microsoft.EntityFrameworkCore;
using Nightfall.Domain;

namespace Nightfall.Infrastructure.History;

public sealed class GameHistoryRepository : IGameHistoryRepository
{
    private readonly NightfallDbContext _db;

    public GameHistoryRepository(NightfallDbContext db)
    {
        _db = db;
    }

    public async Task SaveCompletedGameAsync(GameState game, long telegramChatId, CancellationToken ct = default)
    {
        if (game.CurrentPhase != GamePhase.Ended)
            throw new InvalidOperationException("Only a game that has reached the Ended phase can be saved to history.");

        var record = new GameRecord
        {
            Id = game.GameId,
            TelegramChatId = telegramChatId,
            CreatedAt = game.CreatedAt.UtcDateTime,
            EndedAt = DateTime.UtcNow,
            Result = game.CheckWinCondition(),
            Players = game.Players.Select(p => new GamePlayerRecord
            {
                Id = Guid.NewGuid(),
                PlayerId = p.Id,
                TelegramUsername = p.TelegramUsername,
                Role = p.Role,
                SurvivedToEnd = p.IsAlive,
                GodfatherRank = p.GodfatherRank
            }).ToList()
        };

        _db.Games.Add(record);
        await _db.SaveChangesAsync(ct);
    }

    public Task<GameRecord?> GetAsync(Guid gameId, CancellationToken ct = default) =>
        _db.Games.Include(g => g.Players).FirstOrDefaultAsync(g => g.Id == gameId, ct);

    public async Task<IReadOnlyList<GameRecord>> GetForChatAsync(long telegramChatId, CancellationToken ct = default) =>
        await _db.Games
            .Include(g => g.Players)
            .Where(g => g.TelegramChatId == telegramChatId)
            .OrderByDescending(g => g.EndedAt)
            .ToListAsync(ct);
}
