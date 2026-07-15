using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Nightfall.Api.Auth;

/// <summary>
/// Validates the `initData` string a Telegram Mini App receives from `Telegram.WebApp.initData`,
/// per Telegram's documented algorithm (core.telegram.org/bots/webapps#validating-data-received-via-the-mini-app):
///
///   secret_key = HMAC_SHA256(key: "WebAppData", message: bot_token)
///   data_check_string = all fields except `hash`, sorted by key, joined as "key=value" with '\n'
///   expected_hash = hex(HMAC_SHA256(key: secret_key, message: data_check_string))
///
/// Verified against an independently-generated (Node.js `crypto`, not this codebase) golden
/// fixture — see TelegramInitDataValidatorTests — rather than trusting a single from-memory
/// transcription of an auth-critical algorithm.
/// </summary>
public sealed class TelegramInitDataValidator
{
    private static readonly byte[] WebAppDataKey = Encoding.UTF8.GetBytes("WebAppData");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TelegramAuthOptions _options;

    public TelegramInitDataValidator(IOptions<TelegramAuthOptions> options)
    {
        _options = options.Value;
    }

    public bool TryValidate(string initData, out TelegramWebAppUser? user)
    {
        user = null;
        if (string.IsNullOrWhiteSpace(initData))
            return false;

        var fields = QueryHelpers.ParseQuery(initData);

        if (!fields.TryGetValue("hash", out var providedHashValues))
            return false;
        string providedHash = providedHashValues.ToString();

        byte[] providedHashBytes;
        try
        {
            providedHashBytes = Convert.FromHexString(providedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        string dataCheckString = string.Join('\n',
            fields.Where(kv => kv.Key != "hash")
                  .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                  .Select(kv => $"{kv.Key}={kv.Value}"));

        byte[] secretKey = HmacSha256(WebAppDataKey, Encoding.UTF8.GetBytes(_options.BotToken));
        byte[] computedHash = HmacSha256(secretKey, Encoding.UTF8.GetBytes(dataCheckString));

        if (computedHash.Length != providedHashBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(computedHash, providedHashBytes))
        {
            return false;
        }

        if (!fields.TryGetValue("auth_date", out var authDateValues) ||
            !long.TryParse(authDateValues.ToString(), out long authDateUnix))
        {
            return false;
        }

        var authDate = DateTimeOffset.FromUnixTimeSeconds(authDateUnix);
        if (DateTimeOffset.UtcNow - authDate > _options.MaxInitDataAge)
            return false;

        if (!fields.TryGetValue("user", out var userJson))
            return false;

        try
        {
            user = JsonSerializer.Deserialize<TelegramWebAppUser>(userJson.ToString(), JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        return user is not null;
    }

    private static byte[] HmacSha256(byte[] key, byte[] message)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(message);
    }
}
