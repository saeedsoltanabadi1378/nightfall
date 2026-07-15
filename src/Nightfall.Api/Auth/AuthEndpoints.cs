using Nightfall.Infrastructure.Auth;

namespace Nightfall.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/telegram", (
            TelegramAuthRequest request,
            TelegramInitDataValidator validator,
            JwtTokenService tokenService) =>
        {
            if (!validator.TryValidate(request.InitData, out var user) || user is null)
                return Results.Unauthorized();

            string token = tokenService.IssueToken(user);
            return Results.Ok(new TelegramAuthResponse(token, user.Id, user.Username ?? user.FirstName));
        })
        .WithName("AuthenticateTelegram")
        .AllowAnonymous();
    }
}

public sealed record TelegramAuthRequest(string InitData);

public sealed record TelegramAuthResponse(string Token, long TelegramUserId, string Username);
