using Nightfall.Bot;

namespace Nightfall.Tests;

internal sealed record SentMessage(long ChatId, string Text, string? MiniAppUrl, string? Url = null);

internal sealed class FakeBotMessenger : IBotMessenger
{
    public List<SentMessage> Sent { get; } = new();

    /// <summary>Chat ids for which sending should throw, simulating Telegram refusing to DM a
    /// user who hasn't started a conversation with the bot.</summary>
    public HashSet<long> UnreachableChatIds { get; } = new();

    public Task SendTextAsync(long chatId, string text) => SendWithMiniAppButtonAsync(chatId, text, null);

    public Task SendWithMiniAppButtonAsync(long chatId, string text, string? miniAppUrl)
    {
        if (UnreachableChatIds.Contains(chatId))
            throw new InvalidOperationException("Forbidden: bot can't initiate conversation with a user");

        Sent.Add(new SentMessage(chatId, text, miniAppUrl));
        return Task.CompletedTask;
    }

    public Task SendWithUrlButtonAsync(long chatId, string text, string? url)
    {
        if (UnreachableChatIds.Contains(chatId))
            throw new InvalidOperationException("Forbidden: bot can't initiate conversation with a user");

        Sent.Add(new SentMessage(chatId, text, null, url));
        return Task.CompletedTask;
    }
}
