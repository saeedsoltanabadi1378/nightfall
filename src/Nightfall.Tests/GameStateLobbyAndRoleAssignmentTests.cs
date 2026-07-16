using Nightfall.Domain;

namespace Nightfall.Tests;

public class GameStateLobbyAndRoleAssignmentTests
{
    private static Player NewPlayer(string name = "p") => new(Guid.NewGuid(), name);

    [Fact]
    public void AddPlayer_DuringLobby_Succeeds()
    {
        var game = new GameState();
        var player = NewPlayer();

        game.AddPlayer(player);

        Assert.Single(game.Players);
        Assert.Same(player, game.Players[0]);
    }

    [Fact]
    public void AddPlayer_DuplicateId_Throws()
    {
        var game = new GameState();
        var player = NewPlayer();
        game.AddPlayer(player);

        Assert.Throws<GameException>(() => game.AddPlayer(player));
    }

    [Fact]
    public void AddPlayer_BeyondMaxPlayers_Throws()
    {
        var config = new GameConfig { MinPlayers = 2, MaxPlayers = 2 };
        var game = new GameState(config);
        game.AddPlayer(NewPlayer());
        game.AddPlayer(NewPlayer());

        Assert.Throws<GameException>(() => game.AddPlayer(NewPlayer()));
    }

    [Fact]
    public void AddPlayer_AfterRolesAssigned_Throws()
    {
        var (game, _) = TestGameFactory.CreateNightZeroGame(5);

        Assert.Throws<GameException>(() => game.AddPlayer(NewPlayer()));
    }

    [Fact]
    public void AssignRoles_BelowMinPlayers_Throws()
    {
        var config = new GameConfig { MinPlayers = 5, MaxPlayers = 12 };
        var game = new GameState(config);
        for (int i = 0; i < 4; i++) game.AddPlayer(NewPlayer());

        Assert.Throws<GameException>(() => game.AssignRoles(new Random(1)));
    }

    [Fact]
    public void AssignRoles_WithExplicitOrder_NotMatchingLobby_Throws()
    {
        var game = new GameState();
        for (int i = 0; i < 5; i++) game.AddPlayer(NewPlayer());

        Assert.Throws<GameException>(() => game.AssignRoles(new List<Guid> { Guid.NewGuid() }));
    }

    [Fact]
    public void AssignRoles_MovesPhaseToNightZero()
    {
        var (game, _) = TestGameFactory.CreateNightZeroGame(5);

        Assert.Equal(GamePhase.NightZero, game.CurrentPhase);
    }

    [Fact]
    public void AssignRoles_CalledTwice_Throws()
    {
        var (game, players) = TestGameFactory.CreateAssignedGame(5);

        Assert.Throws<GameException>(() => game.AssignRoles(players.Select(p => p.Id).ToList()));
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(7, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    [InlineData(10, 3)]
    [InlineData(11, 3)]
    [InlineData(12, 4)]
    public void AssignRoles_ProducesExpectedRoleDistribution(int playerCount, int expectedMafiaCount)
    {
        var (_, players) = TestGameFactory.CreateAssignedGame(playerCount);

        Assert.All(players, p => Assert.NotNull(p.Role));
        Assert.Equal(1, players.Count(p => p.Role == Role.Godfather));
        Assert.Equal(expectedMafiaCount - 1, players.Count(p => p.Role == Role.Mafia));
        int expectedTownActors = playerCount == 3 ? 0 : 1;
        Assert.Equal(expectedTownActors, players.Count(p => p.Role == Role.Detective));
        Assert.Equal(expectedTownActors, players.Count(p => p.Role == Role.Doctor));
        Assert.Equal(playerCount - expectedMafiaCount - (expectedTownActors * 2), players.Count(p => p.Role == Role.Villager));

        var mafiaTeam = players.Where(p => p.Role is Role.Mafia or Role.Godfather).ToList();
        Assert.Equal(expectedMafiaCount, mafiaTeam.Count);
        Assert.All(mafiaTeam, p => Assert.NotNull(p.GodfatherRank));
        Assert.Equal(Enumerable.Range(1, expectedMafiaCount), mafiaTeam.OrderBy(p => p.GodfatherRank).Select(p => p.GodfatherRank!.Value));
        Assert.Equal(1, players.Single(p => p.Role == Role.Godfather).GodfatherRank);
    }
}
