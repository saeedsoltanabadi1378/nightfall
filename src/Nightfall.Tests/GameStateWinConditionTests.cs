using Nightfall.Domain;

namespace Nightfall.Tests;

public class GameStateWinConditionTests
{
    [Fact]
    public void CheckWinCondition_FreshlyAssignedGame_IsNone()
    {
        var (game, _) = TestGameFactory.CreateAssignedGame(5);

        Assert.Equal(WinCondition.None, game.CheckWinCondition());
    }

    [Fact]
    public void ResolveNight_MafiaReachesParityWithVillagers_MafiaWinsAndGameEnds()
    {
        // 5 players: 1 Godfather (mafia team) vs Detective, Doctor, Villager, Villager.
        var (game, players) = TestGameFactory.CreateAssignedGame(5);
        var godfather = players.Godfather();
        var nonMafia = players.Where(p => !p.IsMafiaAligned).ToList();

        // Kill villager-aligned players one per night until mafia reaches parity (1 mafia vs 1 villager-aligned).
        for (int i = 0; i < nonMafia.Count - 1; i++)
        {
            game.SubmitNightAction(godfather.Id, nonMafia[i].Id, NightActionType.Kill);
            game.ResolveNight();

            if (game.CurrentPhase == GamePhase.Ended)
            {
                Assert.Equal(WinCondition.MafiaWin, game.CheckWinCondition());
                return;
            }

            game.CycleThroughToNextNight();
        }

        Assert.Fail("Expected the game to end in a Mafia win before exhausting all villager-aligned players.");
    }

    [Fact]
    public void ResolveVoting_LastMafiaEliminated_VillagersWinAndGameEnds()
    {
        // 5 players -> exactly one mafia-aligned player (the Godfather). Voting them out should end the game.
        var (game, players) = TestGameFactory.CreateAssignedGame(5);
        var godfather = players.Godfather();

        game.ResolveNight(); // NightZero, no kills.
        game.StartVoting();

        foreach (var voter in players.Where(p => p.Id != godfather.Id))
        {
            game.SubmitVote(voter.Id, godfather.Id);
        }

        var result = game.ResolveVoting();

        Assert.Equal(godfather.Id, result.Eliminated);
        Assert.Null(result.PromotedGodfatherId); // no Mafia left to succeed the Godfather.
        Assert.Equal(GamePhase.Ended, game.CurrentPhase);
        Assert.Equal(WinCondition.VillagersWin, game.CheckWinCondition());
    }
}
