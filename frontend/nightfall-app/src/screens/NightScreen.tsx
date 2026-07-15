import { useState } from "react";
import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";
import { RoleBanner } from "../components/RoleBanner";
import type { NightActionType } from "../api/types";
import { useLanguage } from "../i18n/LanguageContext";

const actionByRole: Partial<Record<string, { actionType: NightActionType; labelKey: "investigate" | "heal" | "kill"; includeSelf: boolean }>> = {
  Detective: { actionType: "Investigate", labelKey: "investigate", includeSelf: false },
  Doctor: { actionType: "Heal", labelKey: "heal", includeSelf: true },
  Mafia: { actionType: "Kill", labelKey: "kill", includeSelf: false },
  Godfather: { actionType: "Kill", labelKey: "kill", includeSelf: false },
};

export function NightScreen() {
  const { view, busy, actionError, submitNightAction, resolveNight } = useGame();
  const { t } = useLanguage();
  const [selected, setSelected] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  if (!view) return null;

  const isFirstNight = view.phase === "NightZero";
  const action = !isFirstNight && view.yourRole ? actionByRole[view.yourRole] : undefined;
  const targets = view.players.filter((p) => p.isAlive && (action?.includeSelf || p.playerId !== view.yourPlayerId));

  async function handleSubmit() {
    if (!action || !selected) return;
    await submitNightAction(selected, action.actionType);
    setSubmitted(true);
  }

  return (
    <div className="screen">
      <h1>🌙 {isFirstNight ? t("firstNight") : t("night", { number: view.nightNumber })}</h1>

      <RoleBanner role={view.yourRole} alive={view.youAreAlive} />

      {isFirstNight && (
        <div className="banner banner--info">
          {t("firstNightInfo")}
        </div>
      )}

      {view.yourLastInvestigationResult && (
        <div className="banner banner--info">
          {t(view.yourLastInvestigationResult.isMafiaAligned ? "lastInvestigationMafia" : "lastInvestigationTown", {
            name: view.players.find((p) => p.playerId === view.yourLastInvestigationResult!.targetPlayerId)?.telegramUsername ?? t("unknown"),
          })}
        </div>
      )}

      {!view.youAreAlive && <p className="screen__subtitle">{t("deadWatch")}</p>}

      {!isFirstNight && view.youAreAlive && !action && <p className="screen__subtitle">{t("noNightAction")}</p>}

      {view.youAreAlive && action && !submitted && (
        <>
          <p className="screen__subtitle">{t("chooseAction", { action: t(action.labelKey) })}</p>
          <PlayerList players={targets} yourPlayerId={view.yourPlayerId} selectable selectedPlayerId={selected} onSelect={setSelected} />
          {actionError && <div className="banner banner--error">{actionError}</div>}
          <button className="button button--primary" disabled={busy || !selected} onClick={() => void handleSubmit()}>
            {t(action.labelKey)}
          </button>
        </>
      )}

      {view.youAreAlive && action && submitted && <div className="banner banner--info">{t("actionSubmitted")}</div>}

      {view.youAreController && (
        <>
          <hr className="screen__divider" />
          <p className="screen__hint">
            {isFirstNight ? t("endFirstNightHint") : t("endNightHint")}
          </p>
          <button
            className="button button--secondary"
            disabled={busy || (!isFirstNight && !view.requiredNightActionsComplete)}
            onClick={() => void resolveNight()}
          >
            {t("endNight")}
          </button>
        </>
      )}
    </div>
  );
}
