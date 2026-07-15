namespace Nightfall.Api.Games;

public sealed class GameNotFoundException : Exception
{
    public GameNotFoundException(Guid gameId) : base($"Game '{gameId}' was not found.")
    {
    }

    private GameNotFoundException(string message) : base(message)
    {
    }

    public static GameNotFoundException ForChat(long telegramChatId) =>
        new($"No active game was found for chat '{telegramChatId}'.");
}
