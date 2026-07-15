namespace Nightfall.Api.Games;

public sealed class ForbiddenGameActionException : Exception
{
    public ForbiddenGameActionException(string message) : base(message)
    {
    }
}
