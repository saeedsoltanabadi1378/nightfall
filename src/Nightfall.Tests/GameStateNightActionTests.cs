using Nightfall.Domain;

namespace Nightfall.Tests;

public class GameStateNightActionTests
{
    [Fact]
    public void SubmitNightAction_OutsideNightPhase_Throws()
    {
        var game = new GameState();
        game.AddPlayer(new Player(Guid.NewGuid(), "p1"));

        Assert.Throws<GameException>(() => game.SubmitNightAction(Guid.NewGuid(), Guid.NewGuid(), NightActionType.Kill));
    }

    [Fact]
    public void SubmitNightAction_UnknownActor_Throws()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var villager = players.Villagers()[0];

        Assert.Throws<GameException>(() => game.SubmitNightAction(Guid.NewGuid(), villager.Id, NightActionType.Kill));
    }

    [Fact]
    public void SubmitNightAction_DeadActor_Throws()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var godfather = players.Godfather();
        var victim = players.Villagers()[0];
        var doctor = players.Doctor();

        game.SubmitNightAction(godfather.Id, victim.Id, NightActionType.Kill);
        game.ResolveNight();
        game.CycleThroughToNextNight();

        Assert.Throws<GameException>(() => game.SubmitNightAction(victim.Id, doctor.Id, NightActionType.Kill));
    }

    [Fact]
    public void SubmitNightAction_DeadTarget_Throws()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var godfather = players.Godfather();
        var victim = players.Villagers()[0];

        game.SubmitNightAction(godfather.Id, victim.Id, NightActionType.Kill);
        game.ResolveNight();
        game.CycleThroughToNextNight();

        Assert.Throws<GameException>(() => game.SubmitNightAction(godfather.Id, victim.Id, NightActionType.Kill));
    }

    [Theory]
    [InlineData(NightActionType.Investigate)]
    [InlineData(NightActionType.Heal)]
    [InlineData(NightActionType.Kill)]
    public void SubmitNightAction_WrongRoleForAction_Throws(NightActionType actionType)
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var villager = players.Villagers()[0];
        var otherVillager = players.Villagers()[1];

        Assert.Throws<GameException>(() => game.SubmitNightAction(villager.Id, otherVillager.Id, actionType));
    }

    [Fact]
    public void ResolveNight_GodfatherKillWithNoHeal_EliminatesTarget()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var godfather = players.Godfather();
        var victim = players.Villagers()[0];

        game.SubmitNightAction(godfather.Id, victim.Id, NightActionType.Kill);
        var result = game.ResolveNight();

        Assert.Equal(victim.Id, result.Eliminated);
        Assert.False(result.TargetWasSaved);
        Assert.False(victim.IsAlive);
        Assert.Equal(GamePhase.Day, game.CurrentPhase);
    }

    [Fact]
    public void ResolveNight_NoKillSubmitted_NoOneEliminated()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);

        var result = game.ResolveNight();

        Assert.Null(result.Eliminated);
        Assert.False(result.TargetWasSaved);
        Assert.All(players, p => Assert.True(p.IsAlive));
    }

    [Fact]
    public void ResolveNight_DoctorHealsKillTarget_TargetSurvives()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var godfather = players.Godfather();
        var doctor = players.Doctor();
        var victim = players.Villagers()[0];

        game.SubmitNightAction(godfather.Id, victim.Id, NightActionType.Kill);
        game.SubmitNightAction(doctor.Id, victim.Id, NightActionType.Heal);
        var result = game.ResolveNight();

        Assert.Null(result.Eliminated);
        Assert.True(result.TargetWasSaved);
        Assert.True(victim.IsAlive);
    }

    [Fact]
    public void ResolveNight_OnlyGodfatherKillCounts_PlainMafiaTargetIsIgnored()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var godfather = players.Godfather();
        var mafia = players.Mafia()[0];
        var villagers = players.Villagers();

        game.SubmitNightAction(godfather.Id, villagers[0].Id, NightActionType.Kill);
        game.SubmitNightAction(mafia.Id, villagers[1].Id, NightActionType.Kill);
        var result = game.ResolveNight();

        Assert.Equal(villagers[0].Id, result.Eliminated);
        Assert.True(villagers[1].IsAlive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveNight_DetectiveInvestigation_ReturnsCorrectAlignment(bool investigateMafiaAligned)
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var detective = players.Detective();
        var target = investigateMafiaAligned ? players.Mafia()[0] : players.Villagers()[0];

        game.SubmitNightAction(detective.Id, target.Id, NightActionType.Investigate);
        var result = game.ResolveNight();

        Assert.Equal(target.Id, result.DetectiveTarget);
        Assert.Equal(investigateMafiaAligned, result.DetectiveResultIsMafiaAligned);
    }

    [Fact]
    public void SubmitNightAction_DetectiveReInvestigatingSameTarget_Throws()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var detective = players.Detective();
        var target = players.Villagers()[0];

        game.SubmitNightAction(detective.Id, target.Id, NightActionType.Investigate);
        game.ResolveNight();
        game.CycleThroughToNextNight();

        Assert.Throws<GameException>(() => game.SubmitNightAction(detective.Id, target.Id, NightActionType.Investigate));
    }

    [Fact]
    public void SubmitNightAction_DetectiveInvestigatingNewTargetAfterPriorNight_Succeeds()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var detective = players.Detective();
        var villagers = players.Villagers();

        game.SubmitNightAction(detective.Id, villagers[0].Id, NightActionType.Investigate);
        game.ResolveNight();
        game.CycleThroughToNextNight();

        game.SubmitNightAction(detective.Id, villagers[1].Id, NightActionType.Investigate);
        var result = game.ResolveNight();

        Assert.Equal(villagers[1].Id, result.DetectiveTarget);
    }

    [Fact]
    public void SubmitNightAction_DoctorSelfHeal_BlockedOnImmediatelyFollowingNight()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var doctor = players.Doctor();

        game.SubmitNightAction(doctor.Id, doctor.Id, NightActionType.Heal);
        game.ResolveNight();
        game.CycleThroughToNextNight();

        Assert.Throws<GameException>(() => game.SubmitNightAction(doctor.Id, doctor.Id, NightActionType.Heal));
    }

    [Fact]
    public void SubmitNightAction_DoctorSelfHeal_AllowedAgainAfterSkippingANight()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var doctor = players.Doctor();
        var villager = players.Villagers()[0];

        // Night 1: self-heal.
        game.SubmitNightAction(doctor.Id, doctor.Id, NightActionType.Heal);
        game.ResolveNight();
        game.CycleThroughToNextNight();

        // Night 2: heal someone else (self-heal would throw here).
        game.SubmitNightAction(doctor.Id, villager.Id, NightActionType.Heal);
        game.ResolveNight();
        game.CycleThroughToNextNight();

        // Night 3: self-heal is allowed again.
        var exception = Record.Exception(() => game.SubmitNightAction(doctor.Id, doctor.Id, NightActionType.Heal));
        Assert.Null(exception);
    }
}
