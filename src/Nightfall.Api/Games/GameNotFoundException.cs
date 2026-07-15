namespace Nightfall.Api.Games;

public sealed class GameNotFoundException : Exception
{
    public GameNotFoundException(Guid gameId) : base($"Game '{gameId}' was not found.")
    {
    }
}
