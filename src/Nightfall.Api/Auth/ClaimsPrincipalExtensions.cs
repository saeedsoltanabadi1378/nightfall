using System.Security.Claims;

namespace Nightfall.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static long GetTelegramUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(NightfallClaimTypes.TelegramUserId)
            ?? throw new InvalidOperationException($"Authenticated principal is missing the '{NightfallClaimTypes.TelegramUserId}' claim.");
        return long.Parse(claim.Value);
    }

    public static string GetTelegramUsername(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(NightfallClaimTypes.TelegramUsername)
            ?? throw new InvalidOperationException($"Authenticated principal is missing the '{NightfallClaimTypes.TelegramUsername}' claim.");
        return claim.Value;
    }
}
