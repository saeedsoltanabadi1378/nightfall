namespace Nightfall.Bot;

/// <summary>A non-success response from Nightfall.Api, with the server's own error detail (from
/// NightfallExceptionHandler's { title, detail } JSON shape) as a user-facing message the bot can
/// relay directly to the chat, instead of a raw HTTP exception.</summary>
public sealed class NightfallApiException : Exception
{
    public NightfallApiException(string message) : base(message)
    {
    }
}
