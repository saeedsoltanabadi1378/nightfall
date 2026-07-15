namespace Nightfall.Api.Auth;

public sealed class TelegramAuthOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public TimeSpan MaxInitDataAge { get; set; } = TimeSpan.FromHours(24);
}
