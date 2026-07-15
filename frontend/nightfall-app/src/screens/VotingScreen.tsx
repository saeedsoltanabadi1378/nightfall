import { useState } from "react";
import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";
import { RoleBanner } from "../components/RoleBanner";

export function VotingScreen() {
  const { view, busy, actionError, submitVote, resolveVoting } = useGame();
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
      <h1>🗳️ Voting</h1>
      <RoleBanner role={view.yourRole} alive={view.youAreAlive} />

      {!view.youAreAlive && <p className="screen__subtitle">You are no longer able to vote. Watch quietly.</p>}

      {view.youAreAlive && !submitted && (
        <>
          <p className="screen__subtitle">Vote to eliminate someone, or abstain:</p>
          <PlayerList players={candidates} yourPlayerId={view.yourPlayerId} selectable selectedPlayerId={selected} onSelect={setSelected} />
          {actionError && <div className="banner banner--error">{actionError}</div>}
          <button className="button button--primary" disabled={busy || !selected} onClick={() => void handleVote(selected)}>
            Vote
          </button>
          <button className="button button--secondary" disabled={busy} onClick={() => void handleVote(null)}>
            Abstain
          </button>
        </>
      )}

      {view.youAreAlive && submitted && <div className="banner banner--info">Vote submitted. Waiting for the rest of the town…</div>}

      <hr className="screen__divider" />
      <button className="button button--secondary" disabled={busy} onClick={() => void resolveVoting()}>
        Tally votes
      </button>
    </div>
  );
}
