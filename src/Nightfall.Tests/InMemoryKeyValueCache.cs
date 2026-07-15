using Nightfall.Infrastructure.Sessions;

namespace Nightfall.Tests;

internal sealed class InMemoryKeyValueCache : IKeyValueCache
{
    private readonly Dictionary<string, string> _store = new();

    public Task<string?> GetStringAsync(string key) =>
        Task.FromResult(_store.TryGetValue(key, out var value) ? value : null);

    public Task SetStringAsync(string key, string value, TimeSpan? ttl = null)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }
}
