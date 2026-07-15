namespace Nightfall.Infrastructure.Sessions;

/// <summary>A joined player's real Telegram identity for a game. Needed because Player.Id in the
/// Domain/Api is a one-way derived hash (see PlayerIdentity) — nothing can turn it back into a
/// Telegram user id to DM someone. The Bot is the one component that observes real Telegram user
/// ids directly (via Bot API updates), so it's the one that records this mapping.</summary>
public sealed record GameRosterEntry(long TelegramUserId, string Username);
