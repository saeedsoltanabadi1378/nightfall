import type { PlayerView } from "../api/types";
import { roleInfo } from "../lib/gameText";

interface PlayerListProps {
  players: PlayerView[];
  yourPlayerId: string;
  selectable?: boolean;
  selectedPlayerId?: string | null;
  onSelect?: (playerId: string) => void;
  disabledPlayerIds?: string[];
}

export function PlayerList({ players, yourPlayerId, selectable, selectedPlayerId, onSelect, disabledPlayerIds }: PlayerListProps) {
  return (
    <ul className="player-list">
      {players.map((player) => {
        const isYou = player.playerId === yourPlayerId;
        const isDisabled = !player.isAlive || disabledPlayerIds?.includes(player.playerId);
        const isSelected = selectedPlayerId === player.playerId;

        return (
          <li
            key={player.playerId}
            className={[
              "player-list__item",
              !player.isAlive && "player-list__item--dead",
              isSelected && "player-list__item--selected",
              selectable && !isDisabled && "player-list__item--selectable",
            ]
              .filter(Boolean)
              .join(" ")}
            onClick={selectable && !isDisabled ? () => onSelect?.(player.playerId) : undefined}
          >
            <span className="player-list__name">
              {player.telegramUsername}
              {isYou && " (you)"}
            </span>
            {player.revealedRole && (
              <span className="player-list__role">
                {roleInfo[player.revealedRole].emoji} {roleInfo[player.revealedRole].label}
              </span>
            )}
            {!player.isAlive && <span className="player-list__dead-tag">dead</span>}
          </li>
        );
      })}
    </ul>
  );
}
