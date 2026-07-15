using Microsoft.AspNetCore.SignalR;
using Nightfall.Api.Games;
using Nightfall.Domain;

namespace Nightfall.Api.Hubs;

public sealed class SignalRGameNotifier : IGameNotifier
{
    private readonly IHubContext<GameHub> _hubContext;

    public SignalRGameNotifier(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyGameUpdatedAsync(GameState game) =>
        _hubContext.Clients.Group(GameHub.GroupName(game.GameId))
            .SendAsync("GameUpdated", new { gameId = game.GameId, phase = game.CurrentPhase.ToString() });
}
