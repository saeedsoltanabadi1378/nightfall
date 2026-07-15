namespace Nightfall.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "nightfall";
    public string Audience { get; set; } = "nightfall-clients";
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(12);
}
