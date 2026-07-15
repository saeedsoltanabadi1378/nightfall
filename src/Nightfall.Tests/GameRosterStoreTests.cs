using Nightfall.Infrastructure.Sessions;

namespace Nightfall.Tests;

public class GameRosterStoreTests
{
    [Fact]
    public async Task GetAsync_UnknownGame_ReturnsEmpty()
    {
        var store = new GameRosterStore(new InMemoryKeyValueCache());

        var result = await store.GetAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task AddThenGet_RoundTrips()
    {
        var store = new GameRosterStore(new InMemoryKeyValueCache());
        var gameId = Guid.NewGuid();

        await store.AddAsync(gameId, 111, "alice");
        await store.AddAsync(gameId, 222, "bob");
        var result = await store.GetAsync(gameId);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.TelegramUserId == 111 && r.Username == "alice");
        Assert.Contains(result, r => r.TelegramUserId == 222 && r.Username == "bob");
    }

    [Fact]
    public async Task AddAsync_SameTelegramUserIdTwice_DoesNotDuplicate()
    {
        var store = new GameRosterStore(new InMemoryKeyValueCache());
        var gameId = Guid.NewGuid();

        await store.AddAsync(gameId, 111, "alice");
        await store.AddAsync(gameId, 111, "alice");
        var result = await store.GetAsync(gameId);

        Assert.Single(result);
    }

    [Fact]
    public async Task RemoveAsync_ClearsRoster()
    {
        var store = new GameRosterStore(new InMemoryKeyValueCache());
        var gameId = Guid.NewGuid();
        await store.AddAsync(gameId, 111, "alice");

        await store.RemoveAsync(gameId);
        var result = await store.GetAsync(gameId);

        Assert.Empty(result);
    }
}
