using System.Text.Json.Serialization;

namespace Nightfall.Api.Auth;

/// <summary>Shape of the `user` field inside Telegram WebApp initData.</summary>
public sealed record TelegramWebAppUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("language_code")] string? LanguageCode);
