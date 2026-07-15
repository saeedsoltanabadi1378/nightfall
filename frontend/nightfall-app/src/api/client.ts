import type {
  CreateGameResponse,
  GameView,
  NightActionType,
  NightResult,
  TelegramAuthResponse,
  VoiceTokenResponse,
  VotingResult,
} from "./types";

export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

export class NightfallApiClient {
  private readonly baseUrl: string;
  private token: string | null = null;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  setToken(token: string | null) {
    this.token = token;
  }

  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    const headers: Record<string, string> = {};
    if (this.token) {
      headers.Authorization = `Bearer ${this.token}`;
    }
    if (body !== undefined) {
      headers["Content-Type"] = "application/json";
    }

    const response = await fetch(`${this.baseUrl}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });

    if (!response.ok) {
      let detail = response.statusText || `HTTP ${response.status}`;
      try {
        const problem = await response.json();
        if (typeof problem?.detail === "string" && problem.detail.length > 0) {
          detail = problem.detail;
        }
      } catch {
        // no JSON body (e.g. a bare 401 from auth middleware) — fall back to statusText above.
      }
      throw new ApiError(response.status, detail);
    }

    if (response.status === 204) {
      return undefined as T;
    }
    const text = await response.text();
    return text.length > 0 ? (JSON.parse(text) as T) : (undefined as T);
  }

  authenticate(initData: string) {
    return this.request<TelegramAuthResponse>("POST", "/api/auth/telegram", { initData });
  }

  createGame(telegramChatId: number) {
    return this.request<CreateGameResponse>("POST", "/api/games", { telegramChatId });
  }

  joinGame(gameId: string) {
    return this.request<void>("POST", `/api/games/${gameId}/players`);
  }

  startGame(gameId: string) {
    return this.request<void>("POST", `/api/games/${gameId}/start`);
  }

  getGame(gameId: string) {
    return this.request<GameView>("GET", `/api/games/${gameId}`);
  }

  getActiveGameForChat(telegramChatId: number) {
    return this.request<CreateGameResponse>("GET", `/api/games/by-chat/${telegramChatId}`);
  }

  submitNightAction(gameId: string, targetPlayerId: string, actionType: NightActionType) {
    return this.request<void>("POST", `/api/games/${gameId}/night-actions`, { targetPlayerId, actionType });
  }

  resolveNight(gameId: string) {
    return this.request<NightResult>("POST", `/api/games/${gameId}/resolve-night`);
  }

  submitVote(gameId: string, targetPlayerId: string | null) {
    return this.request<void>("POST", `/api/games/${gameId}/votes`, { targetPlayerId });
  }

  resolveVoting(gameId: string) {
    return this.request<VotingResult>("POST", `/api/games/${gameId}/resolve-voting`);
  }

  startVoting(gameId: string) {
    return this.request<void>("POST", `/api/games/${gameId}/start-voting`);
  }

  startNight(gameId: string) {
    return this.request<void>("POST", `/api/games/${gameId}/start-night`);
  }

  getVoiceToken(gameId: string, channel: "main" | "mafia") {
    return this.request<VoiceTokenResponse>("GET", `/api/games/${gameId}/voice-token?channel=${channel}`);
  }
}
