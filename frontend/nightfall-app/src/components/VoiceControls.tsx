import { useEffect, useRef, useState } from "react";
import { useGame } from "../context/useGame";
import { VoiceSession } from "../lib/voiceSession";
import { ApiError } from "../api/client";

type VoiceChannel = "main" | "mafia";

export function VoiceControls() {
  const { view, apiClient } = useGame();
  const sessionRef = useRef<VoiceSession>(new VoiceSession());
  const [connected, setConnected] = useState<VoiceChannel | null>(null);
  const [publishing, setPublishing] = useState(false);
  const [muted, setMuted] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const session = sessionRef.current;
    return () => {
      void session.leave();
    };
  }, []);

  if (!view) return null;

  const isNight = view.phase === "NightZero" || view.phase === "Night";
  const canJoinMafiaChannel = isNight && view.youAreAlive && (view.yourRole === "Mafia" || view.yourRole === "Godfather");

  async function join(channel: VoiceChannel) {
    setBusy(true);
    setError(null);
    try {
      const voiceToken = await apiClient.getVoiceToken(view!.gameId, channel);
      const canPublish = voiceToken.role === "Publisher";
      await sessionRef.current.join(
        import.meta.env.VITE_AGORA_APP_ID,
        voiceToken.channel,
        voiceToken.token,
        voiceToken.uid,
        canPublish,
      );
      setConnected(channel);
      setPublishing(canPublish);
      setMuted(false);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not join voice chat. Check microphone permissions.");
    } finally {
      setBusy(false);
    }
  }

  async function leave() {
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
            {isNight ? "🔇 Listen to room voice" : "🎙️ Join room voice"}
          </button>
          {canJoinMafiaChannel && (
            <button className="button button--voice" disabled={busy} onClick={() => void join("mafia")}>
              🔪 Join Mafia voice
            </button>
          )}
        </div>
      )}

      {connected && (
        <div className="voice-controls__active">
          <span className="voice-controls__status">
            🔊 In {connected === "mafia" ? "Mafia" : "room"} voice{publishing ? "" : " (listening only)"}
          </span>
          {publishing && (
            <button className="button button--voice" disabled={busy} onClick={() => void toggleMute()}>
              {muted ? "🔇 Unmute" : "🎤 Mute"}
            </button>
          )}
          <button className="button button--voice" disabled={busy} onClick={() => void leave()}>
            Leave voice
          </button>
        </div>
      )}
    </div>
  );
}
