using Microsoft.EntityFrameworkCore;
using Nightfall.Domain;
using Nightfall.Infrastructure.Sessions;

namespace Nightfall.Infrastructure.History;

public sealed class GameHistoryRepository : IGameHistoryRepository
{
    private readonly NightfallDbContext _db;
    private readonly IGameRosterStore? _rosterStore;

    public GameHistoryRepository(NightfallDbContext db, IGameRosterStore? rosterStore = null)
    {
        _db = db;
        _rosterStore = rosterStore;
    }

    public async Task SaveCompletedGameAsync(GameState game, CancellationToken ct = default)
    {
        if (game.CurrentPhase != GamePhase.Ended)
            throw new InvalidOperationException("Only a game that has reached the Ended phase can be saved to history.");
        if (game.TelegramChatId is null)
            throw new InvalidOperationException("Game has no TelegramChatId and cannot be saved to history.");

        var roster = _rosterStore is null ? [] : await _rosterStore.GetAsync(game.GameId);
        var record = new GameRecord
        {
            Id = game.GameId,
            TelegramChatId = game.TelegramChatId.Value,
            CreatedAt = game.CreatedAt.UtcDateTime,
            EndedAt = DateTime.UtcNow,
            Result = game.CheckWinCondition(),
            Players = game.Players.Select(p => new GamePlayerRecord
            {
                Id = Guid.NewGuid(),
                PlayerId = p.Id,
                TelegramUserId = roster.FirstOrDefault(r => r.Username == p.TelegramUsername)?.TelegramUserId,
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
