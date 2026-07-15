using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Nightfall.Api.Auth;
using Nightfall.Infrastructure;
using Nightfall.Infrastructure.Sessions;

namespace Nightfall.Api.Hubs;

[Authorize]
public sealed class GameHub : Hub
{
    private readonly IGameSessionStore _sessionStore;

    public GameHub(IGameSessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    /// <summary>Joins the caller's connection to a game's broadcast group, after verifying they're
    /// actually a player in that game (derived from their authenticated Telegram id).</summary>
    public async Task JoinGame(Guid gameId)
    {
        var game = await _sessionStore.GetAsync(gameId) ?? throw new HubException("Game not found.");
        long telegramUserId = Context.User!.GetTelegramUserId();
        var playerId = PlayerIdentity.DeriveId(gameId, telegramUserId);

        if (game.GetPlayer(playerId) is null)
            throw new HubException("You are not a player in this game.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));
    }

    public Task LeaveGame(Guid gameId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(gameId));

    internal static string GroupName(Guid gameId) => $"game:{gameId}";
}
