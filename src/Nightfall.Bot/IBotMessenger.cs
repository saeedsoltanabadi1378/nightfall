namespace Nightfall.Bot;

/// <summary>
/// Thin seam over ITelegramBotClient's message-sending surface, so CommandDispatcher's logic is
/// unit-testable against a simple in-memory fake instead of needing to mock Telegram.Bot's client.
/// </summary>
public interface IBotMessenger
{
    Task SendTextAsync(long chatId, string text);

    /// <summary>Sends text with an optional "Open Nightfall" WebApp button (omitted if miniAppUrl is null).</summary>
    Task SendWithMiniAppButtonAsync(long chatId, string text, string? miniAppUrl);

    /// <summary>Sends a normal URL button. Unlike a WebApp button, Telegram permits this in group chats.</summary>
    Task SendWithUrlButtonAsync(long chatId, string text, string? url);
}
