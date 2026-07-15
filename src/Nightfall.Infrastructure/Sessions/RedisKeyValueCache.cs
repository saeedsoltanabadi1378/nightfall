using StackExchange.Redis;

namespace Nightfall.Infrastructure.Sessions;

public sealed class RedisKeyValueCache : IKeyValueCache
{
    private readonly IConnectionMultiplexer _redis;

    public RedisKeyValueCache(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<string?> GetStringAsync(string key)
    {
        var value = await _redis.GetDatabase().StringGetAsync(key);
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public Task SetStringAsync(string key, string value, TimeSpan? ttl = null) =>
        _redis.GetDatabase().StringSetAsync(key, value, ttl.HasValue ? (Expiration)ttl.Value : Expiration.Default);

    public Task DeleteAsync(string key) =>
        _redis.GetDatabase().KeyDeleteAsync(key);

    public async Task<IReadOnlyList<string>> GetKeysAsync(string pattern)
    {
        var endpoint = _redis.GetEndPoints().FirstOrDefault();
        if (endpoint is null) return [];
        return await _redis.GetServer(endpoint).KeysAsync(pattern: pattern).Select(k => k.ToString()).ToListAsync();
    }
}
