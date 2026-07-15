import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";
import { RoleBanner } from "../components/RoleBanner";
import { useLanguage } from "../i18n/LanguageContext";

export function DayScreen() {
  const { view, busy, actionError, startVoting } = useGame();
  const { t } = useLanguage();
  if (!view) return null;

  const elimination = view.lastNightElimination;
  const eliminatedPlayer = elimination?.eliminatedPlayerId
    ? view.players.find((p) => p.playerId === elimination.eliminatedPlayerId)
    : null;

  return (
    <div className="screen">
      <h1>☀️ {t("day", { number: view.nightNumber })}</h1>
      <RoleBanner role={view.yourRole} alive={view.youAreAlive} />

      {elimination && (
        <div className="banner banner--info">
          {eliminatedPlayer
            ? `${t("foundDead", { name: eliminatedPlayer.telegramUsername })}${
                eliminatedPlayer.revealedRole ? t("theyWere", { role: t(`role${eliminatedPlayer.revealedRole}` as "roleVillager") }) : ""
              }`
            : elimination.wasSaved
              ? t("doctorSaved")
              : t("nobodyDied")}
        </div>
      )}

      <p className="screen__subtitle">{t("discuss")}</p>
      <PlayerList players={view.players} yourPlayerId={view.yourPlayerId} />

      {actionError && <div className="banner banner--error">{actionError}</div>}

      {view.youAreController ? (
        <button className="button button--primary" disabled={busy} onClick={() => void startVoting()}>
          {t("startVoting")}
        </button>
      ) : (
        <p className="screen__hint">{t("waitingVoting")}</p>
      )}
    </div>
  );
}
