import { useState } from "react";
import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";
import { RoleBanner } from "../components/RoleBanner";
import type { NightActionType } from "../api/types";

const actionByRole: Partial<Record<string, { actionType: NightActionType; verb: string; includeSelf: boolean }>> = {
  Detective: { actionType: "Investigate", verb: "Investigate", includeSelf: false },
  Doctor: { actionType: "Heal", verb: "Heal", includeSelf: true },
  Mafia: { actionType: "Kill", verb: "Kill", includeSelf: false },
  Godfather: { actionType: "Kill", verb: "Kill", includeSelf: false },
};

export function NightScreen() {
  const { view, busy, actionError, submitNightAction, resolveNight } = useGame();
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
      <h1>🌙 {view.phase === "NightZero" ? "First Night" : `Night ${view.nightNumber}`}</h1>

      <RoleBanner role={view.yourRole} alive={view.youAreAlive} />

      {isFirstNight && (
        <div className="banner banner--info">
          First night is for Mafia planning only. Detective, Doctor, and Mafia actions begin next night.
        </div>
      )}

      {view.yourLastInvestigationResult && (
        <div className="banner banner--info">
          Last investigation:{" "}
          {view.players.find((p) => p.playerId === view.yourLastInvestigationResult!.targetPlayerId)?.telegramUsername ?? "unknown"} is{" "}
          {view.yourLastInvestigationResult.isMafiaAligned ? "Mafia-aligned" : "not Mafia-aligned"}.
        </div>
      )}

      {!view.youAreAlive && <p className="screen__subtitle">You are no longer able to act. Watch quietly.</p>}

      {!isFirstNight && view.youAreAlive && !action && <p className="screen__subtitle">No night action for your role. Waiting for others…</p>}

      {view.youAreAlive && action && !submitted && (
        <>
          <p className="screen__subtitle">Choose someone to {action.verb.toLowerCase()}:</p>
          <PlayerList players={targets} yourPlayerId={view.yourPlayerId} selectable selectedPlayerId={selected} onSelect={setSelected} />
          {actionError && <div className="banner banner--error">{actionError}</div>}
          <button className="button button--primary" disabled={busy || !selected} onClick={() => void handleSubmit()}>
            {action.verb}
          </button>
        </>
      )}

      {view.youAreAlive && action && submitted && <div className="banner banner--info">Action submitted. Waiting for the night to resolve…</div>}

      {view.youAreController && (
        <>
          <hr className="screen__divider" />
          <p className="screen__hint">
            {isFirstNight ? "End the discussion when the Mafia is ready." : "Night ends after every living night role submits an action."}
          </p>
          <button
            className="button button--secondary"
            disabled={busy || (!isFirstNight && !view.requiredNightActionsComplete)}
            onClick={() => void resolveNight()}
          >
            End night
          </button>
        </>
      )}
    </div>
  );
}
