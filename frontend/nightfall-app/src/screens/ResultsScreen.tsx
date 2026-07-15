import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";
import { RoleBanner } from "../components/RoleBanner";

export function ResultsScreen() {
  const { view, busy, actionError, startNight } = useGame();
  if (!view) return null;

  const elimination = view.lastVotingElimination;
  const eliminatedPlayer = elimination?.eliminatedPlayerId
    ? view.players.find((p) => p.playerId === elimination.eliminatedPlayerId)
    : null;

  return (
    <div className="screen">
      <h1>⚖️ Results</h1>
      <RoleBanner role={view.yourRole} alive={view.youAreAlive} />

      {elimination?.wasTie && <div className="banner banner--info">The vote was tied. Nobody was eliminated.</div>}
      {!elimination?.wasTie && eliminatedPlayer && (
        <div className="banner banner--info">
          The town voted out {eliminatedPlayer.telegramUsername}.
          {eliminatedPlayer.revealedRole && ` They were a ${eliminatedPlayer.revealedRole}.`}
        </div>
      )}
      {!elimination?.wasTie && !eliminatedPlayer && <div className="banner banner--info">No votes were cast. Nobody was eliminated.</div>}

      <PlayerList players={view.players} yourPlayerId={view.yourPlayerId} />

      {actionError && <div className="banner banner--error">{actionError}</div>}

      {view.youAreController ? (
        <button className="button button--primary" disabled={busy} onClick={() => void startNight()}>
          Continue to next night
        </button>
      ) : (
        <p className="screen__hint">Waiting for the game creator to continue.</p>
      )}
    </div>
  );
}
