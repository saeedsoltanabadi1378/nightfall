using Nightfall.Domain;

namespace Nightfall.Infrastructure.History;

/// <summary>Durable record of a finished game, for history/stats/audit. The live GameState during
/// an active game lives in Redis (see Sessions/IGameSessionStore) — this is written once, at game end.</summary>
public sealed class GameRecord
{
    public Guid Id { get; set; }
    public long TelegramChatId { get; set; }

    /// <summary>UTC. Stored as DateTime (not DateTimeOffset) since the Sqlite EF Core provider can't
    /// translate ORDER BY over DateTimeOffset columns; Nightfall always deals in UTC anyway.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public WinCondition Result { get; set; }
    public string Status { get; set; } = "Completed";
    public string? CancellationReason { get; set; }
    public List<GamePlayerRecord> Players { get; set; } = new();
}
