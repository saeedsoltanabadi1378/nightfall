namespace Nightfall.Infrastructure.Agora;

public interface IAgoraTokenService
{
    /// <summary>Mints an RTC voice-channel join token for the given channel/uid/role.</summary>
    string BuildRtcToken(string channelName, uint uid, AgoraRtcRole role, TimeSpan tokenExpiry, TimeSpan privilegeExpiry);
}
