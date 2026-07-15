import { GameProvider } from "./context/GameProvider";
import { useGame } from "./context/useGame";
import { LobbyScreen } from "./screens/LobbyScreen";
import { NightScreen } from "./screens/NightScreen";
import { DayScreen } from "./screens/DayScreen";
import { VotingScreen } from "./screens/VotingScreen";
import { ResultsScreen } from "./screens/ResultsScreen";
import { EndedScreen } from "./screens/EndedScreen";
import { VoiceControls } from "./components/VoiceControls";
import { LanguageToggle } from "./components/LanguageToggle";
import { LanguageProvider, useLanguage } from "./i18n/LanguageContext";
import "./App.css";

function GameRouter() {
  const { status, error, view } = useGame();
  const { t } = useLanguage();

  if (status === "authenticating" || status === "loading-game") {
    return (
      <div className="screen screen--centered">
        <p className="screen__loading">{t("loading")}</p>
      </div>
    );
  }

  if (status === "error") {
    return (
      <div className="screen screen--centered">
        <div className="banner banner--error">{error ?? t("genericError")}</div>
      </div>
    );
  }

  if (status === "no-game") {
    return (
      <div className="screen screen--centered">
        <h1>🌙 Nightfall</h1>
        <p className="screen__subtitle">{t("noGame")}</p>
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
    <LanguageProvider>
      <LanguageToggle />
      <GameProvider>
        <GameRouter />
      </GameProvider>
    </LanguageProvider>
  );
}
