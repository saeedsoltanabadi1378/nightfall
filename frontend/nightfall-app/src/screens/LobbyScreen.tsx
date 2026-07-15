import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";

export function LobbyScreen() {
  const { view, busy, actionError, joinGame, startGame } = useGame();
  if (!view) return null;

  const youHaveJoined = view.players.some((p) => p.playerId === view.yourPlayerId);
  const canStart = view.players.length >= 5;

  return (
    <div className="screen">
      <h1>🌙 Nightfall</h1>
      <p className="screen__subtitle">Waiting in the lobby — {view.players.length} joined.</p>

      <PlayerList players={view.players} yourPlayerId={view.yourPlayerId} />

      {actionError && <div className="banner banner--error">{actionError}</div>}

      {!youHaveJoined && (
        <button className="button button--primary" disabled={busy} onClick={() => void joinGame()}>
          Join lobby
        </button>
      )}

      {youHaveJoined && view.youAreController && (
        <button className="button button--primary" disabled={busy || !canStart} onClick={() => void startGame()}>
          {canStart ? "Start game" : `Need at least 5 players (${view.players.length}/5)`}
        </button>
      )}

      {youHaveJoined && !view.youAreController && (
        <p className="screen__hint">Waiting for the game creator to start.</p>
      )}
    </div>
  );
}
