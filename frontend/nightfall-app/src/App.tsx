import { GameProvider } from "./context/GameProvider";
import { useGame } from "./context/useGame";
import { LobbyScreen } from "./screens/LobbyScreen";
import { NightScreen } from "./screens/NightScreen";
import { DayScreen } from "./screens/DayScreen";
import { VotingScreen } from "./screens/VotingScreen";
import { ResultsScreen } from "./screens/ResultsScreen";
import { EndedScreen } from "./screens/EndedScreen";
import { VoiceControls } from "./components/VoiceControls";
import "./App.css";

function GameRouter() {
  const { status, error, view } = useGame();

  if (status === "authenticating" || status === "loading-game") {
    return (
      <div className="screen screen--centered">
        <p className="screen__loading">🌙 Loading Nightfall…</p>
      </div>
    );
  }

  if (status === "error") {
    return (
      <div className="screen screen--centered">
        <div className="banner banner--error">{error ?? "Something went wrong."}</div>
      </div>
    );
  }

  if (status === "no-game") {
    return (
      <div className="screen screen--centered">
        <h1>🌙 Nightfall</h1>
        <p className="screen__subtitle">
          Open Nightfall from a game's message in your Telegram group — use <code>/newgame</code> or{" "}
          <code>/startgame</code> in the group chat, then tap the button the bot sends.
        </p>
      </div>
    );
  }

  if (!view) return null;

  return (
    <>
      {(view.phase === "Night" || view.phase === "NightZero" || view.phase === "Day" || view.phase === "Voting" || view.phase === "Results") && (
        <VoiceControls />
      )}
      {(view.phase === "Lobby" || view.phase === "RoleAssignment") && <LobbyScreen />}
      {(view.phase === "NightZero" || view.phase === "Night") && <NightScreen />}
      {view.phase === "Day" && <DayScreen />}
      {view.phase === "Voting" && <VotingScreen />}
      {view.phase === "Results" && <ResultsScreen />}
      {view.phase === "Ended" && <EndedScreen />}
    </>
  );
}

export default function App() {
  return (
    <GameProvider>
      <GameRouter />
    </GameProvider>
  );
}
