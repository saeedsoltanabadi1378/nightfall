import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";
import { useLanguage } from "../i18n/LanguageContext";

export function LobbyScreen() {
  const { view, busy, actionError, joinGame, startGame } = useGame();
  const { t } = useLanguage();
  if (!view) return null;

  const youHaveJoined = view.players.some((p) => p.playerId === view.yourPlayerId);
  const canStart = view.players.length >= 5;

  return (
    <div className="screen">
      <h1>🌙 Nightfall</h1>
      <p className="screen__subtitle">{t("waitingLobby", { count: view.players.length })}</p>

      <PlayerList players={view.players} yourPlayerId={view.yourPlayerId} />

      {actionError && <div className="banner banner--error">{actionError}</div>}

      {!youHaveJoined && (
        <button className="button button--primary" disabled={busy} onClick={() => void joinGame()}>
          {t("joinLobby")}
        </button>
      )}

      {youHaveJoined && view.youAreController && (
        <button className="button button--primary" disabled={busy || !canStart} onClick={() => void startGame()}>
          {canStart ? t("startGame") : t("needPlayers", { count: view.players.length })}
        </button>
      )}

      {youHaveJoined && !view.youAreController && (
        <p className="screen__hint">{t("waitingCreatorStart")}</p>
      )}
    </div>
  );
}
