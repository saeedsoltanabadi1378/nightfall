using Microsoft.Extensions.Options;

namespace Nightfall.Infrastructure.Agora;

public sealed class AgoraTokenService : IAgoraTokenService
{
    private readonly AgoraOptions _options;

    public AgoraTokenService(IOptions<AgoraOptions> options)
    {
        _options = options.Value;
    }

    public string BuildRtcToken(string channelName, uint uid, AgoraRtcRole role, TimeSpan tokenExpiry, TimeSpan privilegeExpiry)
    {
        return AgoraAccessToken2.BuildRtcToken(
            _options.AppId,
            _options.AppCertificate,
            channelName,
            uid,
            role,
            (uint)tokenExpiry.TotalSeconds,
            (uint)privilegeExpiry.TotalSeconds,
            issueTs: (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            salt: (uint)Random.Shared.Next());
    }
}
