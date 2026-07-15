using Microsoft.AspNetCore.Authorization;
using Nightfall.Api.Auth;
using Nightfall.Domain;
using Nightfall.Infrastructure;
using Nightfall.Infrastructure.Agora;

namespace Nightfall.Api.Games;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games").RequireAuthorization();

        group.MapPost("/", async (CreateGameRequest request, GameService games, HttpContext http) =>
        {
            var gameId = await games.CreateGameAsync(request.TelegramChatId, http.User.GetTelegramUserId(), http.User.GetTelegramUsername());
            return Results.Ok(new CreateGameResponse(gameId));
        }).WithName("CreateGame");

        group.MapPost("/{gameId:guid}/players", async (Guid gameId, GameService games, HttpContext http) =>
        {
            await games.JoinGameAsync(gameId, http.User.GetTelegramUserId(), http.User.GetTelegramUsername());
            return Results.Ok();
        }).WithName("JoinGame");

        group.MapGet("/{gameId:guid}", async (Guid gameId, GameService games, HttpContext http) =>
        {
            var game = await games.LoadOrThrowAsync(gameId);
            var viewerId = PlayerIdentity.DeriveId(gameId, http.User.GetTelegramUserId());
            return Results.Ok(GameView.For(game, viewerId));
        }).WithName("GetGame");

        group.MapGet("/by-chat/{telegramChatId:long}", async (long telegramChatId, GameService games) =>
        {
            var gameId = await games.GetActiveGameForChatOrThrowAsync(telegramChatId);
            return Results.Ok(new CreateGameResponse(gameId));
        }).WithName("GetActiveGameForChat");

        group.MapPost("/{gameId:guid}/start", async (Guid gameId, GameService games) =>
        {
            await games.StartGameAsync(gameId);
            return Results.Ok();
        }).WithName("StartGame");

        group.MapPost("/{gameId:guid}/night-actions", async (Guid gameId, SubmitNightActionRequest request, GameService games, HttpContext http) =>
        {
            await games.SubmitNightActionAsync(gameId, http.User.GetTelegramUserId(), request.TargetPlayerId, request.ActionType);
            return Results.Ok();
        }).WithName("SubmitNightAction");

        group.MapPost("/{gameId:guid}/resolve-night", async (Guid gameId, GameService games) =>
        {
            var result = await games.ResolveNightAsync(gameId);
            return Results.Ok(result);
        }).WithName("ResolveNight");

        group.MapPost("/{gameId:guid}/votes", async (Guid gameId, SubmitVoteRequest request, GameService games, HttpContext http) =>
        {
            await games.SubmitVoteAsync(gameId, http.User.GetTelegramUserId(), request.TargetPlayerId);
            return Results.Ok();
        }).WithName("SubmitVote");

        group.MapPost("/{gameId:guid}/resolve-voting", async (Guid gameId, GameService games) =>
        {
            var result = await games.ResolveVotingAsync(gameId);
            return Results.Ok(result);
        }).WithName("ResolveVoting");

        group.MapPost("/{gameId:guid}/start-voting", async (Guid gameId, GameService games) =>
        {
            await games.StartVotingAsync(gameId);
            return Results.Ok();
        }).WithName("StartVoting");

        group.MapPost("/{gameId:guid}/start-night", async (Guid gameId, GameService games) =>
        {
            await games.StartNightAsync(gameId);
            return Results.Ok();
        }).WithName("StartNight");

        group.MapGet("/{gameId:guid}/voice-token", async (Guid gameId, string channel, GameService games, IAgoraTokenService agoraTokens, HttpContext http) =>
        {
            var game = await games.LoadOrThrowAsync(gameId);
            long telegramUserId = http.User.GetTelegramUserId();
            var playerId = PlayerIdentity.DeriveId(gameId, telegramUserId);
            var player = game.GetPlayer(playerId) ?? throw new ForbiddenGameActionException("You are not a player in this game.");

            string channelName;
            AgoraRtcRole role;
            switch (channel)
            {
                case "main":
                    channelName = $"nightfall-{gameId}";
                    role = player.IsAlive ? AgoraRtcRole.Publisher : AgoraRtcRole.Subscriber;
                    break;
                case "mafia":
                    if (!player.IsAlive || !player.IsMafiaAligned)
                        throw new ForbiddenGameActionException("Only living Mafia-aligned players may join the Mafia voice channel.");
                    channelName = $"nightfall-{gameId}-mafia";
                    role = AgoraRtcRole.Publisher;
                    break;
                default:
                    return Results.BadRequest(new { detail = "channel must be 'main' or 'mafia'." });
            }

            uint uid = unchecked((uint)telegramUserId);
            string token = agoraTokens.BuildRtcToken(channelName, uid, role, TimeSpan.FromHours(6), TimeSpan.FromHours(6));

            return Results.Ok(new VoiceTokenResponse(token, channelName, uid, role.ToString()));
        }).WithName("GetVoiceToken");
    }
}

public sealed record CreateGameRequest(long TelegramChatId);

public sealed record CreateGameResponse(Guid GameId);

public sealed record SubmitNightActionRequest(Guid TargetPlayerId, NightActionType ActionType);

public sealed record SubmitVoteRequest(Guid? TargetPlayerId);

public sealed record VoiceTokenResponse(string Token, string Channel, uint Uid, string Role);
