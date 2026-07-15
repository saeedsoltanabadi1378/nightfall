using System.Text.Json.Serialization;

namespace Nightfall.Infrastructure.Auth;

/// <summary>
/// An authenticated Telegram identity, regardless of source: either the `user` field inside a
/// Mini App's initData (validated via HMAC by the Api), or the `From` field of a Telegram Bot API
/// Update (trustworthy because it arrives over the bot's authenticated connection to Telegram —
/// the Bot doesn't need a separate HMAC check the way a browser-originated Mini App does).
/// Both shapes carry the same fields, so both can mint the same kind of JWT via JwtTokenService.
/// </summary>
public sealed record TelegramIdentity(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("language_code")] string? LanguageCode);
