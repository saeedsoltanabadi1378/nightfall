using Nightfall.Domain;

namespace Nightfall.Infrastructure.History;

public sealed class GamePlayerRecord
{
    public Guid Id { get; set; }
    public Guid GameRecordId { get; set; }
    public GameRecord GameRecord { get; set; } = null!;
    public Guid PlayerId { get; set; }
    public long? TelegramUserId { get; set; }
    public string TelegramUsername { get; set; } = string.Empty;
    public Role? Role { get; set; }
    public bool SurvivedToEnd { get; set; }
    public int? GodfatherRank { get; set; }
}
