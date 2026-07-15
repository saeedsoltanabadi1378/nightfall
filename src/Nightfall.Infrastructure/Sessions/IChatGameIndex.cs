namespace Nightfall.Infrastructure.Sessions;

/// <summary>Tracks which game is currently active (not yet Ended) for a given Telegram chat, so a
/// caller that only knows the chat id (e.g. the Bot handling a plain "/join" with no game id) can
/// resolve which game that refers to.</summary>
public interface IChatGameIndex
{
    Task SetActiveGameAsync(long telegramChatId, Guid gameId);
    Task<Guid?> GetActiveGameAsync(long telegramChatId);
    Task ClearActiveGameAsync(long telegramChatId);
}
