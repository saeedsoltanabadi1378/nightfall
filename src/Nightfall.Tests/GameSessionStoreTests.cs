using Nightfall.Domain;
using Nightfall.Infrastructure.Sessions;

namespace Nightfall.Tests;

public class GameSessionStoreTests
{
    [Fact]
    public async Task GetAsync_UnknownGame_ReturnsNull()
    {
        var store = new GameSessionStore(new InMemoryKeyValueCache());

        var result = await store.GetAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveThenGet_RoundTripsFullState_IncludingInFlightSubmissions()
    {
        var store = new GameSessionStore(new InMemoryKeyValueCache());
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var godfather = players.Godfather();
        var villager = players.Villagers()[0];

        // Pending, unresolved action -> proves the session store captures in-flight state, not
        // just what's visible from GameState's public API (needed to survive an API/bot restart
        // mid-night without losing a player's submitted-but-unresolved action).
        game.SubmitNightAction(godfather.Id, villager.Id, NightActionType.Kill);

        await store.SaveAsync(game);
        var restored = await store.GetAsync(game.GameId);

        Assert.NotNull(restored);
        Assert.Equal(game.GameId, restored!.GameId);
        Assert.Equal(game.CurrentPhase, restored.CurrentPhase);

        var result = restored.ResolveNight();
        Assert.Equal(villager.Id, result.Eliminated);
    }

    [Fact]
    public async Task RemoveAsync_DeletesSession()
    {
        var store = new GameSessionStore(new InMemoryKeyValueCache());
        var (game, _) = TestGameFactory.CreateAssignedGame(5);
        await store.SaveAsync(game);

        await store.RemoveAsync(game.GameId);
        var result = await store.GetAsync(game.GameId);

        Assert.Null(result);
    }
}
