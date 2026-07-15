namespace Nightfall.Bot;

public sealed class TelegramBotOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
}
