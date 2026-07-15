import type { HubConnection } from "@microsoft/signalr";
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { ApiError, NightfallApiClient } from "../api/client";
import { connectToGameHub } from "../api/signalr";
import type { GameView } from "../api/types";
import { telegram } from "../lib/telegram";
import { GameContext, type GameContextValue, type GameStatus } from "./gameContext";
import { useLanguage } from "../i18n/LanguageContext";

export function GameProvider({ children }: { children: ReactNode }) {
  const { t } = useLanguage();
  const apiClient = useMemo(() => new NightfallApiClient(import.meta.env.VITE_API_BASE_URL), []);

  const [status, setStatus] = useState<GameStatus>("authenticating");
  const [error, setError] = useState<string | null>(null);
  const [view, setView] = useState<GameView | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [gameId, setGameId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const hubRef = useRef<HubConnection | null>(null);
  const telegramUser = telegram.initDataUnsafe.user ?? null;

  const refresh = useCallback(async () => {
    if (!gameId) return;
    try {
      const nextView = await apiClient.getGame(gameId);
      setView(nextView);
    } catch (err) {
      console.error("[Nightfall] Failed to refresh game view", err);
    }
  }, [apiClient, gameId]);

  // Step 1: authenticate with the Api using Telegram's initData.
  useEffect(() => {
    let cancelled = false;

    async function authenticate() {
      if (!telegram.initData) {
        setStatus("error");
        setError(t("noGame"));
        return;
      }
      try {
        const auth = await apiClient.authenticate(telegram.initData);
        if (cancelled) return;
        apiClient.setToken(auth.token);
        setToken(auth.token);
      } catch (err) {
        if (cancelled) return;
        setStatus("error");
        setError(err instanceof ApiError ? err.message : t("genericError"));
      }
    }

    void authenticate();
    return () => {
      cancelled = true;
    };
  }, [apiClient]);

  // Step 2: once authenticated, resolve which game we're looking at (from the bot's deep link
  // start_param — the realistic launch path; see Nightfall.Bot's CommandDispatcher.RevealRolesAsync)
  // and fetch its initial view in the same call.
  useEffect(() => {
    if (!token) return;
    let cancelled = false;

    async function loadInitialGame() {
      setStatus("loading-game");
      const startParamGameId = telegram.initDataUnsafe.start_param;
      if (!startParamGameId) {
        if (!cancelled) {
          setStatus("no-game");
        }
        return;
      }
      try {
        const initialView = await apiClient.getGame(startParamGameId);
        if (cancelled) return;
        setView(initialView);
        setGameId(startParamGameId);
        setStatus("ready");
      } catch (err) {
        if (cancelled) return;
        setStatus("error");
        setError(err instanceof ApiError ? err.message : t("genericError"));
      }
    }

    void loadInitialGame();
    return () => {
      cancelled = true;
    };
  }, [apiClient, token]);

  // Step 3: once we know the game, connect SignalR for live updates.
  useEffect(() => {
    if (!token || !gameId) return;
    let cancelled = false;

    async function connect() {
      try {
        const hub = await connectToGameHub(import.meta.env.VITE_API_BASE_URL, token!, gameId!, () => {
          void refresh();
        });
        if (cancelled) {
          await hub.stop();
          return;
        }
        hubRef.current = hub;
      } catch (err) {
        console.error("[Nightfall] Failed to connect to the live update hub", err);
      }
    }

    void connect();
    return () => {
      cancelled = true;
      void hubRef.current?.stop();
      hubRef.current = null;
    };
  }, [token, gameId, refresh]);

  const runAction = useCallback(async (action: () => Promise<void>) => {
    setBusy(true);
    setActionError(null);
    try {
      await action();
      await refresh();
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : t("genericError"));
    } finally {
      setBusy(false);
    }
  }, [refresh, t]);

  const value: GameContextValue = {
    apiClient,
    status,
    error,
    view,
    telegramUser,
    busy,
    actionError,
    refresh,
    joinGame: () => runAction(() => apiClient.joinGame(gameId!).then(() => undefined)),
    startGame: () => runAction(() => apiClient.startGame(gameId!).then(() => undefined)),
    submitNightAction: (targetPlayerId, actionType) =>
      runAction(() => apiClient.submitNightAction(gameId!, targetPlayerId, actionType).then(() => undefined)),
    resolveNight: () => runAction(() => apiClient.resolveNight(gameId!).then(() => undefined)),
    submitVote: (targetPlayerId) => runAction(() => apiClient.submitVote(gameId!, targetPlayerId).then(() => undefined)),
    resolveVoting: () => runAction(() => apiClient.resolveVoting(gameId!).then(() => undefined)),
    startVoting: () => runAction(() => apiClient.startVoting(gameId!).then(() => undefined)),
    startNight: () => runAction(() => apiClient.startNight(gameId!).then(() => undefined)),
  };

  return <GameContext.Provider value={value}>{children}</GameContext.Provider>;
}
