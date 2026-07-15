using Nightfall.Domain;

namespace Nightfall.Tests;

public class GameStateSnapshotTests
{
    [Fact]
    public void RoundTrip_PreservesPublicState()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var godfather = players.Godfather();
        var victim = players.Villagers()[0];
        game.SubmitNightAction(godfather.Id, victim.Id, NightActionType.Kill);
        game.ResolveNight();

        var restored = GameState.FromSnapshot(game.ToSnapshot());

        Assert.Equal(game.GameId, restored.GameId);
        Assert.Equal(game.CreatedAt, restored.CreatedAt);
        Assert.Equal(game.CurrentPhase, restored.CurrentPhase);
        Assert.Equal(game.NightNumber, restored.NightNumber);
        Assert.Equal(game.Config.MinPlayers, restored.Config.MinPlayers);
        Assert.Equal(game.Config.MaxPlayers, restored.Config.MaxPlayers);
        Assert.Equal(game.Players.Count, restored.Players.Count);
        foreach (var original in game.Players)
        {
            var copy = restored.GetPlayer(original.Id);
            Assert.NotNull(copy);
            Assert.Equal(original.Role, copy!.Role);
            Assert.Equal(original.IsAlive, copy.IsAlive);
            Assert.Equal(original.GodfatherRank, copy.GodfatherRank);
            Assert.Equal(original.TelegramUsername, copy.TelegramUsername);
        }
    }

    [Fact]
    public void RoundTrip_PreservesDetectiveInvestigationHistory()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var detective = players.Detective();
        var target = players.Villagers()[0];

        game.SubmitNightAction(detective.Id, target.Id, NightActionType.Investigate);
        game.ResolveNight();
        game.CycleThroughToNextNight();

        var restored = GameState.FromSnapshot(game.ToSnapshot());

        // The cooldown rule (no re-investigating the same target) must still be enforced post-restore.
        var restoredDetective = restored.GetPlayer(detective.Id)!;
        var restoredTarget = restored.GetPlayer(target.Id)!;
        Assert.Throws<GameException>(() => restored.SubmitNightAction(restoredDetective.Id, restoredTarget.Id, NightActionType.Investigate));
    }

    [Fact]
    public void RoundTrip_PreservesDoctorSelfHealCooldown()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var doctor = players.Doctor();

        game.SubmitNightAction(doctor.Id, doctor.Id, NightActionType.Heal);
        game.ResolveNight();
        game.CycleThroughToNextNight();

        var restored = GameState.FromSnapshot(game.ToSnapshot());
        var restoredDoctor = restored.GetPlayer(doctor.Id)!;

        Assert.Throws<GameException>(() => restored.SubmitNightAction(restoredDoctor.Id, restoredDoctor.Id, NightActionType.Heal));
    }

    [Fact]
    public void RoundTrip_PreservesPendingUnresolvedNightActionsAndVotes()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var godfather = players.Godfather();
        var villager = players.Villagers()[0];

        // Submit a night action but do NOT resolve it, so it's still "pending" in the snapshot.
        game.SubmitNightAction(godfather.Id, villager.Id, NightActionType.Kill);

        var restored = GameState.FromSnapshot(game.ToSnapshot());
        var result = restored.ResolveNight();

        Assert.Equal(villager.Id, result.Eliminated);
    }
}
