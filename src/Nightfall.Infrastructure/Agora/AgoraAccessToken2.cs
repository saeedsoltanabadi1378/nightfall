using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Nightfall.Infrastructure.Agora;

/// <summary>
/// C# port of Agora's official AccessToken2 ("007") algorithm, scoped to RTC tokens only
/// (Nightfall doesn't use Agora RTM/Chat/FPA/Education services).
///
/// Ported from the MIT-licensed reference at github.com/AgoraIO/Tools
/// (DynamicKey/AgoraDynamicKey/csharp/src/AgoraIO/Media/AccessToken2.cs +
/// TokenBuilders/RtcTokenBuilder2.cs), since there is no official Agora .NET SDK.
/// Verified byte-for-byte against the reference implementation with fixed issueTs/salt
/// (see AgoraAccessToken2Tests) rather than trusting a hand transcription of the spec.
///
/// Wire format: "007" + base64(zlib(signature-bytes ++ header-bytes)), where header-bytes is
/// appId ++ issueTs ++ tokenExpire ++ salt ++ serviceCount ++ [service]*, and signature is
/// HMAC-SHA256 chained: HMAC(LE(issueTs), UTF8(appCertificate)) -> HMAC(LE(salt), that) -> HMAC(that, header-bytes).
/// All multi-byte integers are little-endian; strings/byte blobs are uint16-length-prefixed.
/// </summary>
public static class AgoraAccessToken2
{
    private const string Version = "007";

    private enum ServiceType : ushort
    {
        Rtc = 1
    }

    private enum RtcPrivilege : ushort
    {
        JoinChannel = 1,
        PublishAudioStream = 2,
        PublishVideoStream = 3,
        PublishDataStream = 4
    }

    public static string BuildRtcToken(
        string appId,
        string appCertificate,
        string channelName,
        uint uid,
        AgoraRtcRole role,
        uint tokenExpireSeconds,
        uint privilegeExpireSeconds,
        uint issueTs,
        uint salt)
    {
        ValidateHex32(appId, nameof(appId));
        ValidateHex32(appCertificate, nameof(appCertificate));
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        string uidText = uid == 0 ? string.Empty : uid.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var privileges = new Dictionary<ushort, uint> { [(ushort)RtcPrivilege.JoinChannel] = privilegeExpireSeconds };
        if (role == AgoraRtcRole.Publisher)
        {
            privileges[(ushort)RtcPrivilege.PublishAudioStream] = privilegeExpireSeconds;
            privileges[(ushort)RtcPrivilege.PublishVideoStream] = privilegeExpireSeconds;
            privileges[(ushort)RtcPrivilege.PublishDataStream] = privilegeExpireSeconds;
        }

        byte[] serviceBytes = new AgoraByteWriter()
            .WriteUInt16((ushort)ServiceType.Rtc)
            .WritePrivilegeMap(privileges)
            .WriteString(channelName)
            .WriteString(uidText)
            .ToArray();

        byte[] header = new AgoraByteWriter()
            .WriteString(appId)
            .WriteUInt32(issueTs)
            .WriteUInt32(tokenExpireSeconds)
            .WriteUInt32(salt)
            .WriteUInt16(1) // exactly one service: RTC
            .WriteRaw(serviceBytes)
            .ToArray();

        byte[] signature = ComputeSignature(appCertificate, issueTs, salt, header);

        byte[] content = new AgoraByteWriter()
            .WriteBytes(signature)
            .WriteRaw(header)
            .ToArray();

        return Version + Convert.ToBase64String(ZlibCompress(content));
    }

    private static byte[] ComputeSignature(string appCertificate, uint issueTs, uint salt, byte[] header)
    {
        byte[] signing = Hmac(LittleEndianBytes(issueTs), Encoding.UTF8.GetBytes(appCertificate));
        byte[] signatureKey = Hmac(LittleEndianBytes(salt), signing);
        return Hmac(signatureKey, header);
    }

    private static byte[] Hmac(byte[] key, byte[] message)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(message);
    }

    private static byte[] LittleEndianBytes(uint value) =>
        new[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) };

    private static byte[] ZlibCompress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private static void ValidateHex32(string value, string paramName)
    {
        if (value.Length != 32 || !value.All(Uri.IsHexDigit))
            throw new ArgumentException("Must be a 32-character hexadecimal string.", paramName);
    }
}
