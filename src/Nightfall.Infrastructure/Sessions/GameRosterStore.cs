using System.Text.Json;

namespace Nightfall.Infrastructure.Sessions;

public sealed class GameRosterStore : IGameRosterStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(6);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IKeyValueCache _cache;

    public GameRosterStore(IKeyValueCache cache)
    {
        _cache = cache;
    }

    private static string KeyFor(Guid gameId) => $"nightfall:roster:{gameId}";

    public async Task AddAsync(Guid gameId, long telegramUserId, string username)
    {
        var roster = (await GetAsync(gameId)).ToList();
        if (roster.Any(r => r.TelegramUserId == telegramUserId))
            return;

        roster.Add(new GameRosterEntry(telegramUserId, username));
        await _cache.SetStringAsync(KeyFor(gameId), JsonSerializer.Serialize(roster, JsonOptions), Ttl);
    }

    public async Task<IReadOnlyList<GameRosterEntry>> GetAsync(Guid gameId)
    {
        var json = await _cache.GetStringAsync(KeyFor(gameId));
        if (json is null)
            return Array.Empty<GameRosterEntry>();

        return JsonSerializer.Deserialize<List<GameRosterEntry>>(json, JsonOptions) ?? new List<GameRosterEntry>();
    }

    public Task RemoveAsync(Guid gameId) => _cache.DeleteAsync(KeyFor(gameId));
}
