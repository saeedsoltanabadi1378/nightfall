namespace Nightfall.Api.Admin;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
