import { createContext } from "react";
import type { NightfallApiClient } from "../api/client";
import type { GameView, NightActionType } from "../api/types";
import type { TelegramUser } from "../lib/telegram";

export type GameStatus = "authenticating" | "loading-game" | "ready" | "no-game" | "error";

export interface GameContextValue {
  apiClient: NightfallApiClient;
  status: GameStatus;
  error: string | null;
  view: GameView | null;
  telegramUser: TelegramUser | null;
  busy: boolean;
  actionError: string | null;
  refresh: () => Promise<void>;
  joinGame: () => Promise<void>;
  startGame: () => Promise<void>;
  submitNightAction: (targetPlayerId: string, actionType: NightActionType) => Promise<void>;
  resolveNight: () => Promise<void>;
  submitVote: (targetPlayerId: string | null) => Promise<void>;
  resolveVoting: () => Promise<void>;
  startVoting: () => Promise<void>;
  startNight: () => Promise<void>;
}

export const GameContext = createContext<GameContextValue | null>(null);
