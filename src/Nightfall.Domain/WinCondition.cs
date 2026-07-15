using System.Text.Json.Serialization;

namespace Nightfall.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WinCondition
{
    None,
    VillagersWin,
    MafiaWin
}
