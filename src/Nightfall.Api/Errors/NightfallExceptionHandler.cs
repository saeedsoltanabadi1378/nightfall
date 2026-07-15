using Microsoft.AspNetCore.Diagnostics;
using Nightfall.Api.Games;
using Nightfall.Domain;

namespace Nightfall.Api.Errors;

/// <summary>Central mapping from domain/application exceptions to HTTP responses, so endpoint
/// handlers don't each need their own try/catch for "this is just a rule violation, return 400".</summary>
public sealed class NightfallExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            GameNotFoundException => (StatusCodes.Status404NotFound, "Game not found"),
            GameException => (StatusCodes.Status400BadRequest, "Invalid game action"),
            ForbiddenGameActionException => (StatusCodes.Status403Forbidden, "Forbidden"),
            _ => (0, string.Empty)
        };

        if (statusCode == 0)
            return false;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            title,
            detail = exception.Message
        }, cancellationToken);

        return true;
    }
}
