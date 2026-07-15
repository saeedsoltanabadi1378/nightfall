import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";
import { useLanguage } from "../i18n/LanguageContext";

export function EndedScreen() {
  const { view } = useGame();
  const { t } = useLanguage();
  if (!view) return null;

  const winnerLabel = view.winCondition === "VillagersWin" ? t("villagersWin") : view.winCondition === "MafiaWin" ? t("mafiaWins") : null;

  return (
    <div className="screen">
      <h1>🏆 {t("gameOver")}</h1>
      {winnerLabel && <div className="banner banner--info">{winnerLabel}</div>}
      <PlayerList players={view.players} yourPlayerId={view.yourPlayerId} />
    </div>
  );
}
