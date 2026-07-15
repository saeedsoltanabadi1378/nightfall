using Nightfall.Infrastructure.Sessions;

namespace Nightfall.Tests;

public class ChatGameIndexTests
{
    [Fact]
    public async Task GetActiveGameAsync_UnknownChat_ReturnsNull()
    {
        var index = new ChatGameIndex(new InMemoryKeyValueCache());

        var result = await index.GetActiveGameAsync(12345);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetThenGet_RoundTrips()
    {
        var index = new ChatGameIndex(new InMemoryKeyValueCache());
        var gameId = Guid.NewGuid();

        await index.SetActiveGameAsync(12345, gameId);
        var result = await index.GetActiveGameAsync(12345);

        Assert.Equal(gameId, result);
    }

    [Fact]
    public async Task ClearActiveGameAsync_RemovesEntry()
    {
        var index = new ChatGameIndex(new InMemoryKeyValueCache());
        var gameId = Guid.NewGuid();
        await index.SetActiveGameAsync(12345, gameId);

        await index.ClearActiveGameAsync(12345);
        var result = await index.GetActiveGameAsync(12345);

        Assert.Null(result);
    }

    [Fact]
    public async Task DifferentChats_AreIndependent()
    {
        var index = new ChatGameIndex(new InMemoryKeyValueCache());
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();

        await index.SetActiveGameAsync(1, gameA);
        await index.SetActiveGameAsync(2, gameB);

        Assert.Equal(gameA, await index.GetActiveGameAsync(1));
        Assert.Equal(gameB, await index.GetActiveGameAsync(2));
    }
}
