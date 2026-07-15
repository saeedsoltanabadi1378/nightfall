namespace Nightfall.Infrastructure.Sessions;

/// <summary>
/// Minimal string key/value cache abstraction. Kept separate from StackExchange.Redis's IDatabase
/// (100+ methods) so GameSessionStore's logic can be unit tested against a trivial in-memory fake
/// instead of requiring a live Redis instance or a mocking library.
/// </summary>
public interface IKeyValueCache
{
    Task<string?> GetStringAsync(string key);
    Task SetStringAsync(string key, string value, TimeSpan? ttl = null);
    Task DeleteAsync(string key);
    Task<IReadOnlyList<string>> GetKeysAsync(string pattern);
}
