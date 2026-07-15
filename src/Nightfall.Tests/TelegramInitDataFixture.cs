using System.Security.Cryptography;
using System.Text;

namespace Nightfall.Tests;

/// <summary>
/// Signs a Telegram WebApp initData string for arbitrary test users, so integration tests can
/// drive the real POST /api/auth/telegram endpoint instead of minting JWTs directly. The
/// signing algorithm itself is already independently verified against a Node.js-generated golden
/// fixture in TelegramInitDataValidatorTests — this just needs to produce input that endpoint
/// accepts, for tests that are about gameplay flow, not about auth correctness.
/// </summary>
internal static class TelegramInitDataFixture
{
    public static string Sign(string botToken, long telegramUserId, string username, long? authDateUnix = null)
    {
        long authDate = authDateUnix ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string userJson = $$"""{"id":{{telegramUserId}},"first_name":"{{username}}","username":"{{username}}"}""";

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = authDate.ToString(),
            ["query_id"] = "AAHtest",
            ["user"] = userJson
        };

        string dataCheckString = string.Join('\n', fields.Select(kv => $"{kv.Key}={kv.Value}"));

        byte[] secretKey = Hmac(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        byte[] hash = Hmac(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        string hashHex = Convert.ToHexStringLower(hash);

        return string.Join('&', fields.Append(new KeyValuePair<string, string>("hash", hashHex))
            .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
    }

    private static byte[] Hmac(byte[] key, byte[] message)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(message);
    }
}
