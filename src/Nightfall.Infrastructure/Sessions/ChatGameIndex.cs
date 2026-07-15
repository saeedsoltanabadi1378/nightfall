namespace Nightfall.Infrastructure.Sessions;

public sealed class ChatGameIndex : IChatGameIndex
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(6);

    private readonly IKeyValueCache _cache;

    public ChatGameIndex(IKeyValueCache cache)
    {
        _cache = cache;
    }

    private static string KeyFor(long telegramChatId) => $"nightfall:chat-active-game:{telegramChatId}";

    public Task SetActiveGameAsync(long telegramChatId, Guid gameId) =>
        _cache.SetStringAsync(KeyFor(telegramChatId), gameId.ToString(), Ttl);

    public async Task<Guid?> GetActiveGameAsync(long telegramChatId)
    {
        var value = await _cache.GetStringAsync(KeyFor(telegramChatId));
        return value is not null && Guid.TryParse(value, out var gameId) ? gameId : null;
    }

    public Task ClearActiveGameAsync(long telegramChatId) => _cache.DeleteAsync(KeyFor(telegramChatId));
}
