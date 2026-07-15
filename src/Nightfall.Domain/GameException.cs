namespace Nightfall.Domain;

/// <summary>Thrown when an operation would violate a game rule (invalid phase, role, or target).</summary>
public sealed class GameException : Exception
{
    public GameException(string message) : base(message)
    {
    }
}
