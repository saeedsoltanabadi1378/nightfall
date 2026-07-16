using Nightfall.Domain;

namespace Nightfall.Tests;

public class GameStateGodfatherSuccessionTests
{
    [Fact]
    public void PromoteGodfather_PromotesLowestRankedAliveMafiaMember()
    {
        // 12 players -> mafiaCount == 4: rank 1 (Godfather), then ranks 2 through 4 (Mafia).
        var (game, players) = TestGameFactory.CreateAssignedGame(12);
        var godfather = players.Godfather();
        var mafia = players.Mafia(); // ordered by rank: [rank 2, rank 3, rank 4]

        game.ResolveNight();
        game.StartVoting();
        foreach (var voter in players.Where(p => p.Id != godfather.Id))
        {
            game.SubmitVote(voter.Id, godfather.Id);
        }
        game.ResolveVoting(); // internally promotes rank 2 already; rank 2 is no longer Role.Mafia.

        // Calling PromoteGodfather() again exercises its selection logic directly: with rank 2 now
        // holding Role.Godfather, the next-lowest-ranked alive Mafia member (rank 3) must be chosen.
        var promoted = game.PromoteGodfather();

        Assert.NotNull(promoted);
        Assert.Equal(mafia[1].Id, promoted!.Id); // rank 3, not rank 2 (already promoted) or anyone else.
        Assert.Equal(Role.Godfather, promoted.Role);
    }

    [Fact]
    public void ResolveVoting_GodfatherEliminated_PromotesNextInRankOrder()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(12);
        var godfather = players.Godfather();
        var mafia = players.Mafia(); // [rank 2, rank 3, rank 4]

        game.ResolveNight(); // NightZero, no actions.
        game.StartVoting();
        foreach (var voter in players.Where(p => p.Id != godfather.Id))
        {
            game.SubmitVote(voter.Id, godfather.Id);
        }
        var result = game.ResolveVoting();

        Assert.Equal(godfather.Id, result.Eliminated);
        Assert.Equal(mafia[0].Id, result.PromotedGodfatherId); // rank 2, not rank 3.
        Assert.Equal(Role.Godfather, mafia[0].Role);
        Assert.Equal(Role.Mafia, mafia[1].Role); // rank 3 stays a plain Mafia member.
        Assert.False(godfather.IsAlive);
        Assert.NotEqual(GamePhase.Ended, game.CurrentPhase); // mafia team still has three living members.
    }

    [Fact]
    public void SuccessionChain_FollowsPreAssignedRankAcrossMultipleEliminations()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(12);
        var originalGodfather = players.Godfather();
        var mafia = players.Mafia(); // [rank 2, rank 3, rank 4]
        var rank2 = mafia[0];
        var rank3 = mafia[1];

        game.ResolveNight();

        // Round 1: eliminate the original Godfather (rank 1) -> rank 2 should be promoted.
        game.StartVoting();
        foreach (var voter in players.Where(p => p.Id != originalGodfather.Id))
        {
            game.SubmitVote(voter.Id, originalGodfather.Id);
        }
        var firstResult = game.ResolveVoting();
        Assert.Equal(rank2.Id, firstResult.PromotedGodfatherId);
        Assert.Equal(Role.Godfather, rank2.Role);

        // Round 2: eliminate the newly promoted Godfather (was rank 2) -> rank 3 should be promoted next,
        // proving succession follows the rank assigned at game start, not join/vote order.
        game.StartNight();
        game.ResolveNight();
        game.StartVoting();
        foreach (var voter in players.Where(p => p.IsAlive && p.Id != rank2.Id))
        {
            game.SubmitVote(voter.Id, rank2.Id);
        }
        var secondResult = game.ResolveVoting();

        Assert.Equal(rank2.Id, secondResult.Eliminated);
        Assert.Equal(rank3.Id, secondResult.PromotedGodfatherId);
        Assert.Equal(Role.Godfather, rank3.Role);
    }

    [Fact]
    public void PromoteGodfather_NoRemainingMafia_ReturnsNull()
    {
        // 5 players -> mafiaCount == 1, so the Godfather has no successor.
        var (game, players) = TestGameFactory.CreateAssignedGame(5);

        var promoted = game.PromoteGodfather();

        Assert.Null(promoted);
    }
}
