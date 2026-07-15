import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";
import { RoleBanner } from "../components/RoleBanner";
import { useLanguage } from "../i18n/LanguageContext";

export function ResultsScreen() {
  const { view, busy, actionError, startNight } = useGame();
  const { t } = useLanguage();
  if (!view) return null;

  const elimination = view.lastVotingElimination;
  const eliminatedPlayer = elimination?.eliminatedPlayerId
    ? view.players.find((p) => p.playerId === elimination.eliminatedPlayerId)
    : null;

  return (
    <div className="screen">
      <h1>⚖️ {t("results")}</h1>
      <RoleBanner role={view.yourRole} alive={view.youAreAlive} />

      {elimination?.wasTie && <div className="banner banner--info">{t("voteTie")}</div>}
      {!elimination?.wasTie && eliminatedPlayer && (
        <div className="banner banner--info">
          {t("votedOut", { name: eliminatedPlayer.telegramUsername })}
          {eliminatedPlayer.revealedRole && t("theyWere", { role: t(`role${eliminatedPlayer.revealedRole}` as "roleVillager") })}
        </div>
      )}
      {!elimination?.wasTie && !eliminatedPlayer && <div className="banner banner--info">{t("noVotes")}</div>}

      <PlayerList players={view.players} yourPlayerId={view.yourPlayerId} />

      {actionError && <div className="banner banner--error">{actionError}</div>}

      {view.youAreController ? (
        <button className="button button--primary" disabled={busy} onClick={() => void startNight()}>
          {t("nextNight")}
        </button>
      ) : (
        <p className="screen__hint">{t("waitingContinue")}</p>
      )}
    </div>
  );
}
