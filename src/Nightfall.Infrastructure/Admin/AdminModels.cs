using System.Text.Json;

namespace Nightfall.Infrastructure.Admin;

public sealed class BotSettingsRecord
{
    public int Id { get; set; } = 1;
    public int Version { get; set; } = 1;
    public int MinPlayers { get; set; } = 5;
    public int MaxPlayers { get; set; } = 12;
    public bool MaintenanceMode { get; set; }
    public string MaintenanceMessage { get; set; } = "Nightfall is temporarily unavailable for maintenance.";
    public string EnabledCommandsJson { get; set; } = JsonSerializer.Serialize(BotSettingsDefaults.Commands);
    public bool SoloTestEnabled { get; set; }
    public string MiniAppBaseUrl { get; set; } = string.Empty;
    public string HelpMessage { get; set; } = BotSettingsDefaults.HelpMessage;
    public string WelcomeMessage { get; set; } = "🌙 New Nightfall game created by {creator}! Open the lobby to join, or use /join. Once everyone's in, /startgame.";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class BotSettingsDefaults
{
    public static readonly string[] Commands = ["newgame", "join", "startgame", "resolvenight", "startvoting", "resolvevoting", "startnight", "myrole", "solotest", "solonext", "help", "start"];
    public const string HelpMessage = "Nightfall commands:\n/newgame — start a new lobby in this chat\n/join — join the current lobby\n/startgame — assign roles and begin\n/resolvenight — resolve the current night\n/startvoting — open voting\n/resolvevoting — tally votes\n/startnight — begin the next night\n/myrole — DM yourself your current role";
}

public sealed class UserProfileRecord { public long TelegramUserId { get; set; } public string Username { get; set; } = string.Empty; public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow; public DateTime LastSeenAt { get; set; } = DateTime.UtcNow; }
public sealed class ChatProfileRecord { public long TelegramChatId { get; set; } public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow; public DateTime LastSeenAt { get; set; } = DateTime.UtcNow; }
public sealed class OperationalEventRecord { public long Id { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; public string Category { get; set; } = string.Empty; public string Severity { get; set; } = "Information"; public string Message { get; set; } = string.Empty; public string? TargetType { get; set; } public string? TargetId { get; set; } public string? MetadataJson { get; set; } public bool IsAdminAudit { get; set; } }
