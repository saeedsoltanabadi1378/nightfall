using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nightfall.Domain;
using Nightfall.Infrastructure.Auth;

namespace Nightfall.Bot;

/// <summary>
/// Typed HTTP client for Nightfall.Api, acting on behalf of a specific Telegram user. Mints a
/// short-lived JWT per call via JwtTokenService (the same signing key the Api validates against),
/// rather than performing the Mini-App initData round trip — the Bot already knows the caller is
/// legitimate because Telegram's Bot API only delivers a message's `From` field over the bot's own
/// authenticated connection, which is a comparable trust boundary to a validated initData HMAC.
/// </summary>
public sealed class NightfallApiClient : INightfallApiClient
{
    private readonly HttpClient _http;
    private readonly JwtTokenService _tokenService;

    public NightfallApiClient(HttpClient http, JwtTokenService tokenService)
    {
        _http = http;
        _tokenService = tokenService;
    }

    public async Task<Guid> CreateGameAsync(long telegramChatId, TelegramIdentity actor)
    {
        var response = await SendAsync<CreateGameResponseDto>(HttpMethod.Post, "/api/games", actor, new { telegramChatId });
        return response.GameId;
    }

    public Task JoinGameAsync(Guid gameId, TelegramIdentity actor) =>
        SendAsync(HttpMethod.Post, $"/api/games/{gameId}/players", actor);

    public Task StartGameAsync(Guid gameId, TelegramIdentity actor) =>
        SendAsync(HttpMethod.Post, $"/api/games/{gameId}/start", actor);

    public Task<GameViewDto> GetGameAsync(Guid gameId, TelegramIdentity actor) =>
        SendAsync<GameViewDto>(HttpMethod.Get, $"/api/games/{gameId}", actor);

    public async Task<Guid?> GetActiveGameForChatAsync(long telegramChatId, TelegramIdentity actor)
    {
        try
        {
            var result = await SendAsync<CreateGameResponseDto>(HttpMethod.Get, $"/api/games/by-chat/{telegramChatId}", actor);
            return result.GameId;
        }
        catch (NightfallApiException)
        {
            return null;
        }
    }

    public Task SubmitNightActionAsync(Guid gameId, TelegramIdentity actor, Guid targetPlayerId, NightActionType actionType) =>
        SendAsync(HttpMethod.Post, $"/api/games/{gameId}/night-actions", actor, new { targetPlayerId, actionType });

    public Task<NightResult> ResolveNightAsync(Guid gameId, TelegramIdentity actor) =>
        SendAsync<NightResult>(HttpMethod.Post, $"/api/games/{gameId}/resolve-night", actor);

    public Task SubmitVoteAsync(Guid gameId, TelegramIdentity actor, Guid? targetPlayerId) =>
        SendAsync(HttpMethod.Post, $"/api/games/{gameId}/votes", actor, new { targetPlayerId });

    public Task<VotingResult> ResolveVotingAsync(Guid gameId, TelegramIdentity actor) =>
        SendAsync<VotingResult>(HttpMethod.Post, $"/api/games/{gameId}/resolve-voting", actor);

    public Task StartVotingAsync(Guid gameId, TelegramIdentity actor) =>
        SendAsync(HttpMethod.Post, $"/api/games/{gameId}/start-voting", actor);

    public Task StartNightAsync(Guid gameId, TelegramIdentity actor) =>
        SendAsync(HttpMethod.Post, $"/api/games/{gameId}/start-night", actor);

    private async Task SendAsync(HttpMethod method, string path, TelegramIdentity actor, object? body = null)
    {
        using var response = await SendRequestAsync(method, path, actor, body);
        await EnsureSuccessAsync(response);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, TelegramIdentity actor, object? body = null)
    {
        using var response = await SendRequestAsync(method, path, actor, body);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string path, TelegramIdentity actor, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenService.IssueToken(actor));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return await _http.SendAsync(request);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        string detail = response.StatusCode.ToString();
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiErrorBody>();
            if (problem?.Detail is { Length: > 0 })
            {
                detail = problem.Detail;
            }
        }
        catch
        {
            // ignore — fall back to the status code text below.
        }

        throw new NightfallApiException(detail);
    }

    private sealed record ApiErrorBody(string? Title, string? Detail);
}
