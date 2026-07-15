import { useEffect, useState } from "react";
import { useGame } from "../context/useGame";
import { PlayerList } from "../components/PlayerList";
import { RoleBanner } from "../components/RoleBanner";
import { useLanguage } from "../i18n/LanguageContext";

export function DayScreen() {
  const { view, busy, actionError, requestChallenge, cancelChallenge, acceptChallenge, rejectChallenge, finishDiscussion } = useGame();
  const { t } = useLanguage();
  const [now, setNow] = useState(Date.now());
  useEffect(() => { const id = window.setInterval(() => setNow(Date.now()), 250); return () => clearInterval(id); }, []);
  if (!view) return null;

  const elimination = view.lastNightElimination;
  const eliminatedPlayer = elimination?.eliminatedPlayerId ? view.players.find((p) => p.playerId === elimination.eliminatedPlayerId) : null;
  const discussion = view.discussion;
  const active = discussion ? view.players.find(p => p.playerId === discussion.activePlayerId) : null;
  const seconds = discussion ? Math.max(0, Math.ceil((new Date(discussion.deadline).getTime() - now) / 1000)) : 0;

  return (
    <div className="screen">
      <h1>☀️ {t("day", { number: view.nightNumber })}</h1>
      <RoleBanner role={view.yourRole} alive={view.youAreAlive} />
      {elimination && <div className="banner banner--info">{eliminatedPlayer
        ? `${t("foundDead", { name: eliminatedPlayer.telegramUsername })}${eliminatedPlayer.revealedRole ? t("theyWere", { role: t(`role${eliminatedPlayer.revealedRole}` as "roleVillager") }) : ""}`
        : elimination.wasSaved ? t("doctorSaved") : t("nobodyDied")}</div>}

      {discussion && active && <section className="discussion-card">
        <span className="discussion-card__kind">{discussion.segmentType === "Challenge" ? t("challengeTurn") : t("speakerTurn")}</span>
        <strong>{active.telegramUsername}</strong>
        <time>{seconds}</time>
        {discussion.youCanFinish && <button className="button button--primary" disabled={busy} onClick={() => void finishDiscussion()}>{t("finishSpeaking")}</button>}
        {discussion.youCanRequestChallenge && <button className="button button--secondary" disabled={busy} onClick={() => void requestChallenge()}>{t("requestChallenge")}</button>}
        {discussion.yourChallengeIsPending && <button className="button button--secondary" disabled={busy} onClick={() => void cancelChallenge()}>{t("cancelChallenge")}</button>}
        {discussion.pendingChallengerIds.length > 0 && <div className="challenge-list">
          <b>{t("challengeRequests")}</b>
          {discussion.pendingChallengerIds.map(id => <div key={id}><span>{view.players.find(p => p.playerId === id)?.telegramUsername}</span><button disabled={busy} onClick={() => void acceptChallenge(id)}>{t("accept")}</button><button disabled={busy} onClick={() => void rejectChallenge(id)}>{t("reject")}</button></div>)}
        </div>}
      </section>}

      <p className="screen__subtitle">{t("timedDiscuss")}</p>
      <PlayerList players={view.players} yourPlayerId={view.yourPlayerId} />
      {actionError && <div className="banner banner--error">{actionError}</div>}
    </div>
  );
}
