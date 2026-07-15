namespace Nightfall.Infrastructure.Agora;

public sealed class AgoraOptions
{
    public const string SectionName = "Agora";

    public string AppId { get; set; } = string.Empty;
    public string AppCertificate { get; set; } = string.Empty;
}
