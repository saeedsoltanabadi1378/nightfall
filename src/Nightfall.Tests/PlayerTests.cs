using Nightfall.Domain;

namespace Nightfall.Tests;

public class PlayerTests
{
    [Fact]
    public void Constructor_WithEmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Player(Guid.Empty, "someone"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithoutUsername_Throws(string? username)
    {
        Assert.Throws<ArgumentException>(() => new Player(Guid.NewGuid(), username!));
    }

    [Fact]
    public void NewPlayer_StartsAliveWithNoRoleAssigned()
    {
        var player = new Player(Guid.NewGuid(), "someone");

        Assert.True(player.IsAlive);
        Assert.Null(player.Role);
        Assert.Null(player.GodfatherRank);
        Assert.False(player.IsMafiaAligned);
    }

    [Theory]
    [InlineData(Role.Mafia, true)]
    [InlineData(Role.Godfather, true)]
    [InlineData(Role.Villager, false)]
    [InlineData(Role.Detective, false)]
    [InlineData(Role.Doctor, false)]
    public void IsMafiaAligned_ReflectsRole(Role role, bool expected)
    {
        // 7 players -> mafiaCount == 2, so every role in the enum is represented.
        var (_, players) = TestGameFactory.CreateAssignedGame(7);
        var target = players.First(p => p.Role == role);

        Assert.Equal(expected, target.IsMafiaAligned);
    }
}
