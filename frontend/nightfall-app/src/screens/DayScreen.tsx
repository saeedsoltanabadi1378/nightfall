import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";
import { RoleBanner } from "../components/RoleBanner";

export function DayScreen() {
  const { view, busy, actionError, startVoting } = useGame();
  if (!view) return null;

  const elimination = view.lastNightElimination;
  const eliminatedPlayer = elimination?.eliminatedPlayerId
    ? view.players.find((p) => p.playerId === elimination.eliminatedPlayerId)
    : null;

  return (
    <div className="screen">
      <h1>☀️ Day {view.nightNumber}</h1>
      <RoleBanner role={view.yourRole} alive={view.youAreAlive} />

      {elimination && (
        <div className="banner banner--info">
          {eliminatedPlayer
            ? `${eliminatedPlayer.telegramUsername} was found dead this morning.${
                eliminatedPlayer.revealedRole ? ` They were a ${eliminatedPlayer.revealedRole}.` : ""
              }`
            : elimination.wasSaved
              ? "The Doctor saved the target! Nobody died last night."
              : "Nobody died last night."}
        </div>
      )}

      <p className="screen__subtitle">Discuss, then move to voting when ready.</p>
      <PlayerList players={view.players} yourPlayerId={view.yourPlayerId} />

      {actionError && <div className="banner banner--error">{actionError}</div>}

      <button className="button button--primary" disabled={busy} onClick={() => void startVoting()}>
        Start voting
      </button>
    </div>
  );
}
