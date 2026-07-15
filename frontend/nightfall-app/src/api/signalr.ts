import * as signalR from "@microsoft/signalr";

/**
 * Connects to Nightfall.Api's GameHub and joins a single game's broadcast group. The hub only
 * ever sends a lightweight "GameUpdated" ping (see Nightfall.Api.Hubs.GameHub /
 * SignalRGameNotifier) — callers should re-fetch the filtered GET /api/games/{id} view in
 * response, never trust a payload pushed over the shared group channel.
 */
export async function connectToGameHub(
  baseUrl: string,
  token: string,
  gameId: string,
  onGameUpdated: () => void,
): Promise<signalR.HubConnection> {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${baseUrl}/hubs/game`, {
      accessTokenFactory: () => token,
    })
    .withAutomaticReconnect()
    .build();

  connection.on("GameUpdated", onGameUpdated);

  connection.onreconnected(() => {
    connection.invoke("JoinGame", gameId).catch((err) => console.error("[Nightfall] Failed to rejoin game group after reconnect", err));
    onGameUpdated();
  });

  await connection.start();
  await connection.invoke("JoinGame", gameId);

  return connection;
}
