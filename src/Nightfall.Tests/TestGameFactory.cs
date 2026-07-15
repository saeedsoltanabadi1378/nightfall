using Nightfall.Domain;

namespace Nightfall.Tests;

/// <summary>
/// Builds games with deterministic role assignment (via GameState.AssignRoles(IReadOnlyList&lt;Guid&gt;))
/// so tests can address players by their known role instead of relying on randomness.
/// Assignment order fills Godfather, then Mafia (rank 2, 3, ...), then Detective, Doctor, then Villagers.
/// </summary>
internal static class TestGameFactory
{
    public static (GameState Game, List<Player> Players) CreateAssignedGame(int playerCount, GameConfig? config = null, long? telegramChatId = null)
    {
        var result = CreateNightZeroGame(playerCount, config, telegramChatId);
        result.Game.AdvanceToFirstActionNight();
        return result;
    }

    public static (GameState Game, List<Player> Players) CreateNightZeroGame(int playerCount, GameConfig? config = null, long? telegramChatId = null)
    {
        var game = new GameState(config, telegramChatId: telegramChatId);
        var players = Enumerable.Range(1, playerCount)
            .Select(i => new Player(Guid.NewGuid(), $"player{i}"))
            .ToList();

        foreach (var player in players)
        {
            game.AddPlayer(player);
        }

        game.AssignRoles(players.Select(p => p.Id).ToList());

        return (game, players);
    }

    public static Player Godfather(this List<Player> players) => players.Single(p => p.Role == Role.Godfather);
    public static List<Player> Mafia(this List<Player> players) => players.Where(p => p.Role == Role.Mafia).OrderBy(p => p.GodfatherRank).ToList();
    public static Player Detective(this List<Player> players) => players.Single(p => p.Role == Role.Detective);
    public static Player Doctor(this List<Player> players) => players.Single(p => p.Role == Role.Doctor);
    public static List<Player> Villagers(this List<Player> players) => players.Where(p => p.Role == Role.Villager).ToList();

    /// <summary>Ends discussion-only Night Zero and advances through an empty day/vote to the first actionable Night.</summary>
    public static void AdvanceToFirstActionNight(this GameState game)
    {
        game.ResolveNight();
        game.StartVoting();
        game.ResolveVoting();
        game.StartNight();
    }

    /// <summary>Runs a Day → Voting → Results → Night cycle with no votes cast, leaving the game ready for the next SubmitNightAction/ResolveNight.</summary>
    public static void CycleThroughToNextNight(this GameState game)
    {
        game.StartVoting();
        game.ResolveVoting();
        game.StartNight();
    }
}
