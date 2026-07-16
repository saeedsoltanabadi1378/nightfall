namespace Nightfall.Domain;

public sealed class GameConfig
{
    public int MinPlayers { get; init; } = 3;
    public int MaxPlayers { get; init; } = 12;

    public static GameConfig Default { get; } = new();
}
