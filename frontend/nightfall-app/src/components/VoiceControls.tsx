import { useCallback, useEffect, useRef, useState } from "react";
import { useGame } from "../context/useGame";
import { VoiceSession } from "../lib/voiceSession";
import { ApiError } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

type VoiceChannel = "main" | "mafia";

export function VoiceControls() {
  const { view, apiClient } = useGame();
  const { t } = useLanguage();
  const sessionRef = useRef<VoiceSession>(new VoiceSession());
  const [connected, setConnected] = useState<VoiceChannel | null>(null);
  const [publishing, setPublishing] = useState(false);
  const [muted, setMuted] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const switchVersionRef = useRef(0);
  const voiceGameId = view?.gameId;
  const voicePhase = view?.phase;
  const activeDiscussionPlayer = view?.discussion?.activePlayerId;

  useEffect(() => {
    const session = sessionRef.current;
    return () => {
      void session.leave();
    };
  }, []);

  const join = useCallback(async (channel: VoiceChannel) => {
    if (!voiceGameId) return;
    const switchVersion = ++switchVersionRef.current;
    setBusy(true);
    setError(null);
    try {
      const voiceToken = await apiClient.getVoiceToken(voiceGameId, channel);
      const canPublish = voiceToken.role === "Publisher";
      await sessionRef.current.join(
        import.meta.env.VITE_AGORA_APP_ID,
        voiceToken.channel,
        voiceToken.token,
        voiceToken.uid,
        canPublish,
      );
      if (switchVersion !== switchVersionRef.current) return;
      setConnected(channel);
      setPublishing(canPublish);
      setMuted(false);
    } catch (err) {
      if (switchVersion !== switchVersionRef.current) return;
      setConnected(null);
      setPublishing(false);
      setError(err instanceof ApiError ? err.message : t("voiceError"));
    } finally {
      if (switchVersion === switchVersionRef.current) setBusy(false);
    }
  }, [apiClient, t, voiceGameId]);

  const isNight = voicePhase === "NightZero" || voicePhase === "Night";
  const canJoinMafiaChannel = Boolean(isNight && view?.youAreAlive && (view.yourRole === "Mafia" || view.yourRole === "Godfather"));

  // Voice follows the game phase automatically. At night, ordinary/dead players move to the
  // listen-only public channel while living Mafia members move to their private channel. As soon
  // as night resolves, everyone returns to the public channel; the API decides whether they may
  // publish (living daytime players) or only listen (eliminated players).
  useEffect(() => {
    if (!voiceGameId) return;
    const target: VoiceChannel = canJoinMafiaChannel ? "mafia" : "main";
    void join(target);
  }, [voiceGameId, voicePhase, activeDiscussionPlayer, canJoinMafiaChannel, join]);

  if (!view) return null;

  async function leave() {
    ++switchVersionRef.current;
    setBusy(true);
    try {
      await sessionRef.current.leave();
      setConnected(null);
      setPublishing(false);
    } finally {
      setBusy(false);
    }
  }

  async function toggleMute() {
    const next = !muted;
    await sessionRef.current.setMuted(next);
    setMuted(next);
  }

  return (
    <div className="voice-controls">
      {error && <div className="banner banner--error">{error}</div>}

      {!connected && (
        <div className="voice-controls__buttons">
          <button className="button button--voice" disabled={busy} onClick={() => void join("main")}>
            {isNight ? t("listenRoom") : t("joinRoom")}
          </button>
          {canJoinMafiaChannel && (
            <button className="button button--voice" disabled={busy} onClick={() => void join("mafia")}>
              {t("joinMafia")}
            </button>
          )}
        </div>
      )}

      {connected && (
        <div className="voice-controls__active">
          <span className="voice-controls__status">
            {t(connected === "mafia" ? "inMafiaVoice" : "inRoomVoice")}{publishing ? "" : t("listeningOnly")}
          </span>
          {publishing && (
            <button className="button button--voice" disabled={busy} onClick={() => void toggleMute()}>
              {muted ? t("unmute") : t("mute")}
            </button>
          )}
          <button className="button button--voice" disabled={busy} onClick={() => void leave()}>
            {t("leaveVoice")}
          </button>
        </div>
      )}
    </div>
  );
}
