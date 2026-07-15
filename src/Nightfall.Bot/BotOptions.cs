namespace Nightfall.Bot;

public sealed class BotOptions
{
    public const string SectionName = "Bot";

    public string NightfallApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Base URL for the Mini App WebApp button (e.g. https://t.me/YourBot/app). Optional —
    /// the frontend isn't built yet as of this phase, so DM role-reveal falls back to plain text
    /// without a button when this isn't configured.</summary>
    public string? MiniAppBaseUrl { get; set; }
}
