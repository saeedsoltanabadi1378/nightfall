namespace Nightfall.Domain;

public sealed class Player
{
    public Guid Id { get; }
    public string TelegramUsername { get; }
    public Role? Role { get; private set; }
    public bool IsAlive { get; private set; } = true;

    /// <summary>Succession order within the Mafia team, assigned at role-assignment time. 1 = starts as Godfather.</summary>
    public int? GodfatherRank { get; private set; }

    public Player(Guid id, string telegramUsername)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Player id must not be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(telegramUsername))
            throw new ArgumentException("Telegram username is required.", nameof(telegramUsername));

        Id = id;
        TelegramUsername = telegramUsername;
    }

    public bool IsMafiaAligned => Role == Nightfall.Domain.Role.Mafia || Role == Nightfall.Domain.Role.Godfather;

    internal void AssignRole(Role role) => Role = role;

    internal void SetGodfatherRank(int? rank) => GodfatherRank = rank;

    internal void Eliminate() => IsAlive = false;
}
