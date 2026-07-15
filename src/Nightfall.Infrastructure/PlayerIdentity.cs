using System.Security.Cryptography;
using System.Text;

namespace Nightfall.Infrastructure;

/// <summary>
/// Deterministically derives a Domain Player.Id from (gameId, telegramUserId), instead of storing
/// a separate telegram-user-to-player-id mapping. This lets the API derive "which Player am I in
/// this game" from an authenticated caller's Telegram id + the game route parameter alone — it
/// never trusts a client-supplied actor id for "act as yourself" operations (submit my vote/night
/// action). Not security-sensitive by itself (the id isn't a secret; JWT auth is what actually
/// gates who can act), just needs to be stable and collision-free per (gameId, telegramUserId).
/// </summary>
public static class PlayerIdentity
{
    public static Guid DeriveId(Guid gameId, long telegramUserId)
    {
        Span<byte> input = stackalloc byte[24];
        gameId.TryWriteBytes(input);
        BitConverter.TryWriteBytes(input[16..], telegramUserId);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);

        return new Guid(hash[..16]);
    }
}
