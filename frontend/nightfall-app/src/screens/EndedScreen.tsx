import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";

export function EndedScreen() {
  const { view } = useGame();
  if (!view) return null;

  const winnerLabel = view.winCondition === "VillagersWin" ? "The Villagers win!" : view.winCondition === "MafiaWin" ? "The Mafia wins!" : null;

  return (
    <div className="screen">
      <h1>🏆 Game Over</h1>
      {winnerLabel && <div className="banner banner--info">{winnerLabel}</div>}
      <PlayerList players={view.players} yourPlayerId={view.yourPlayerId} />
    </div>
  );
}
