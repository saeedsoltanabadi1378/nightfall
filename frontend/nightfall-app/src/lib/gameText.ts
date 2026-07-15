import type { Role } from "../api/types";

export const roleInfo: Record<Role, { emoji: string; label: string; description: string }> = {
  Villager: { emoji: "👤", label: "Villager", description: "Work with the town to find the Mafia." },
  Detective: { emoji: "🔍", label: "Detective", description: "Each night, investigate one player to learn if they're Mafia-aligned." },
  Doctor: { emoji: "💉", label: "Doctor", description: "Each night, choose one player to protect from the Mafia's kill." },
  Mafia: { emoji: "🔪", label: "Mafia", description: "Work with your team to eliminate the Villagers without being caught." },
  Godfather: { emoji: "👑", label: "Godfather", description: "Leader of the Mafia. Choose who dies each night." },
};

export const phaseLabel: Record<string, string> = {
  Lobby: "Lobby",
  RoleAssignment: "Assigning roles…",
  NightZero: "First Night",
  Night: "Night",
  Day: "Day",
  Voting: "Voting",
  Results: "Results",
  Ended: "Game Over",
};
