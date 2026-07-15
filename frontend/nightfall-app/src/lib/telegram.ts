export interface TelegramUser {
  id: number;
  first_name: string;
  last_name?: string;
  username?: string;
  language_code?: string;
}

export interface TelegramWebApp {
  initData: string;
  initDataUnsafe: {
    user?: TelegramUser;
    start_param?: string;
    chat?: { id: number };
  };
  colorScheme: "light" | "dark";
  ready(): void;
  expand(): void;
  close(): void;
  HapticFeedback?: {
    notificationOccurred(type: "error" | "success" | "warning"): void;
  };
}

declare global {
  interface Window {
    Telegram?: { WebApp: TelegramWebApp };
  }
}

/**
 * Outside Telegram (plain browser dev), window.Telegram is undefined. This mock lets the app
 * render for local UI development — it can't produce a real initData signature (that requires
 * the bot's secret token), so auth against a real backend will correctly 401 unless the backend
 * is configured with a matching known dev bot token and this initData is generated to match
 * (see the frontend README / Phase 5 smoke-test notes for how to do that for a genuine E2E check).
 */
function createMockWebApp(): TelegramWebApp {
  console.warn("[Nightfall] Not running inside Telegram — using a mock WebApp for local UI development.");
  const params = new URLSearchParams(window.location.search);
  return {
    initData: params.get("mockInitData") ?? "",
    initDataUnsafe: {
      user: { id: 111111111, first_name: "Dev", username: "dev_user" },
      start_param: params.get("gameId") ?? undefined,
    },
    colorScheme: "dark",
    ready() {},
    expand() {},
    close() {
      console.warn("[Nightfall] WebApp.close() called (mock).");
    },
  };
}

function resolveWebApp(): TelegramWebApp {
  const params = new URLSearchParams(window.location.search);
  // An explicit dev override always wins, since the real telegram-web-app.js script defines
  // window.Telegram.WebApp even outside an actual Telegram client — as an inert stub with an
  // empty initData — so a plain `?? createMockWebApp()` fallback would never actually trigger.
  if (params.has("mockInitData") || params.has("gameId")) {
    return createMockWebApp();
  }

  const real = window.Telegram?.WebApp;
  return real?.initData ? real : createMockWebApp();
}

export const telegram: TelegramWebApp = resolveWebApp();
