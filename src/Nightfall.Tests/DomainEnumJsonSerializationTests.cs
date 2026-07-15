using System.Text.Json;
using Nightfall.Domain;

namespace Nightfall.Tests;

// Locks in the wire-format contract: Domain enums serialize as readable strings (via
// [JsonConverter(typeof(JsonStringEnumConverter))] on each enum), not raw ordinals — the
// frontend and any other client shouldn't have to hardcode magic numbers that would silently
// break if an enum is reordered. Uses JsonSerializer.Serialize directly (bypassing HTTP) so this
// is a true test of the type-level attribute, not something that could pass either way depending
// on whether client and server just happen to agree on numeric defaults.
public class DomainEnumJsonSerializationTests
{
    [Fact]
    public void GamePhase_SerializesAsString()
    {
        Assert.Equal("\"Night\"", JsonSerializer.Serialize(GamePhase.Night));
    }

    [Fact]
    public void Role_SerializesAsString()
    {
        Assert.Equal("\"Godfather\"", JsonSerializer.Serialize(Role.Godfather));
    }

    [Fact]
    public void NightActionType_SerializesAsString()
    {
        Assert.Equal("\"Investigate\"", JsonSerializer.Serialize(NightActionType.Investigate));
    }

    [Fact]
    public void WinCondition_SerializesAsString()
    {
        Assert.Equal("\"MafiaWin\"", JsonSerializer.Serialize(WinCondition.MafiaWin));
    }

    [Fact]
    public void GamePhase_RoundTripsFromString()
    {
        var deserialized = JsonSerializer.Deserialize<GamePhase>("\"Voting\"");

        Assert.Equal(GamePhase.Voting, deserialized);
    }
}
