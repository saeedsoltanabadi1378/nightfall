import { useState } from "react";
import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";
import { RoleBanner } from "../components/RoleBanner";
import { useLanguage } from "../i18n/LanguageContext";

export function VotingScreen() {
  const { view, busy, actionError, submitVote, resolveVoting } = useGame();
  const { t } = useLanguage();
  const [selected, setSelected] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  if (!view) return null;

  const candidates = view.players.filter((p) => p.isAlive);

  async function handleVote(targetPlayerId: string | null) {
    await submitVote(targetPlayerId);
    setSubmitted(true);
  }

  return (
    <div className="screen">
      <h1>🗳️ {t("voting")}</h1>
      <RoleBanner role={view.yourRole} alive={view.youAreAlive} />

      {!view.youAreAlive && <p className="screen__subtitle">{t("deadCannotVote")}</p>}

      {view.youAreAlive && !submitted && (
        <>
          <p className="screen__subtitle">{t("votePrompt")}</p>
          <PlayerList players={candidates} yourPlayerId={view.yourPlayerId} selectable selectedPlayerId={selected} onSelect={setSelected} />
          {actionError && <div className="banner banner--error">{actionError}</div>}
          <button className="button button--primary" disabled={busy || !selected} onClick={() => void handleVote(selected)}>
            {t("vote")}
          </button>
          <button className="button button--secondary" disabled={busy} onClick={() => void handleVote(null)}>
            {t("abstain")}
          </button>
        </>
      )}

      {view.youAreAlive && submitted && <div className="banner banner--info">{t("voteSubmitted")}</div>}

      {view.youAreController && (
        <>
          <hr className="screen__divider" />
          <button className="button button--secondary" disabled={busy} onClick={() => void resolveVoting()}>
            {t("tallyVotes")}
          </button>
        </>
      )}
    </div>
  );
}
