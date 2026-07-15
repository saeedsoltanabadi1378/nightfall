using Nightfall.Domain;

namespace Nightfall.Api.Games;

/// <summary>Pushes a lightweight "something changed" signal to a game's connected clients, which
/// then re-fetch GET /api/games/{id} for their own per-viewer-filtered state. Deliberately never
/// broadcasts full game state over the shared group channel — that would mean picking one payload
/// for every connection, and there's no safe shared payload once roles/investigation results are
/// involved (see GameView).</summary>
public interface IGameNotifier
{
    Task NotifyGameUpdatedAsync(GameState game);
}
