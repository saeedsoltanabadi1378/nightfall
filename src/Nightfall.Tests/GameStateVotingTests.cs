using Nightfall.Domain;

namespace Nightfall.Tests;

public class GameStateVotingTests
{
    private static (GameState Game, List<Player> Players) CreateGameInVoting(int playerCount)
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(playerCount);
        game.ResolveNight(); // NightZero, no actions submitted
        game.StartVoting();
        return (game, players);
    }

    [Fact]
    public void SubmitVote_OutsideVotingPhase_Throws()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);

        Assert.Throws<GameException>(() => game.SubmitVote(players[0].Id, players[1].Id));
    }

    [Fact]
    public void SubmitVote_Abstain_IsAccepted()
    {
        var (game, players) = CreateGameInVoting(9);

        var exception = Record.Exception(() => game.SubmitVote(players[0].Id, null));

        Assert.Null(exception);
    }

    [Fact]
    public void SubmitVote_UnknownTarget_Throws()
    {
        var (game, players) = CreateGameInVoting(9);
        var alive = players.First(p => p.IsAlive);

        Assert.Throws<GameException>(() => game.SubmitVote(alive.Id, Guid.NewGuid()));
    }

    [Fact]
    public void SubmitVote_DeadTarget_Throws()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(9);
        var godfather = players.Godfather();
        var victim = players.Villagers()[0];

        game.SubmitNightAction(godfather.Id, victim.Id, NightActionType.Kill);
        game.ResolveNight();
        game.StartVoting();

        var alive = players.First(p => p.IsAlive);
        Assert.Throws<GameException>(() => game.SubmitVote(alive.Id, victim.Id));
    }

    [Fact]
    public void ResolveVoting_MajorityTarget_IsEliminated()
    {
        var (game, players) = CreateGameInVoting(9);
        var target = players.Villagers()[0];
        var voters = players.Where(p => p.Id != target.Id).ToList();

        // 5 votes for target, rest split/abstain -> clear majority.
        for (int i = 0; i < 5; i++)
        {
            game.SubmitVote(voters[i].Id, target.Id);
        }
        for (int i = 5; i < voters.Count; i++)
        {
            game.SubmitVote(voters[i].Id, null);
        }

        var result = game.ResolveVoting();

        Assert.Equal(target.Id, result.Eliminated);
        Assert.False(result.WasTie);
        Assert.False(target.IsAlive);
        Assert.Equal(GamePhase.Results, game.CurrentPhase);
    }

    [Fact]
    public void ResolveVoting_TiedVotes_NoOneIsEliminated()
    {
        var (game, players) = CreateGameInVoting(9);
        var villagers = players.Villagers();
        var candidateA = villagers[0];
        var candidateB = villagers[1];
        var others = players.Where(p => p.Id != candidateA.Id && p.Id != candidateB.Id).ToList();

        game.SubmitVote(others[0].Id, candidateA.Id);
        game.SubmitVote(others[1].Id, candidateA.Id);
        game.SubmitVote(others[2].Id, candidateB.Id);
        game.SubmitVote(others[3].Id, candidateB.Id);

        var result = game.ResolveVoting();

        Assert.Null(result.Eliminated);
        Assert.True(result.WasTie);
        Assert.Equal(new[] { candidateA.Id, candidateB.Id }.OrderBy(id => id), result.TiedPlayers.OrderBy(id => id));
        Assert.True(candidateA.IsAlive);
        Assert.True(candidateB.IsAlive);
        Assert.Equal(GamePhase.Results, game.CurrentPhase);
    }

    [Fact]
    public void ResolveVoting_NoVotesCast_NoOneIsEliminated()
    {
        var (game, players) = CreateGameInVoting(9);

        var result = game.ResolveVoting();

        Assert.Null(result.Eliminated);
        Assert.False(result.WasTie);
        Assert.Empty(result.TiedPlayers);
        Assert.All(players, p => Assert.True(p.IsAlive));
    }
}
