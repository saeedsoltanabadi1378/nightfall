using System.Text.Json.Serialization;

namespace Nightfall.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Role
{
    Villager,
    Detective,
    Doctor,
    Mafia,
    Godfather
}
