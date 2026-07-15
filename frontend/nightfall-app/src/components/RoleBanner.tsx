import type { Role } from "../api/types";
import { roleInfo } from "../lib/gameText";

export function RoleBanner({ role, alive }: { role: Role | null; alive: boolean }) {
  if (!role) return null;
  const info = roleInfo[role];

  return (
    <div className={`role-banner${alive ? "" : " role-banner--dead"}`}>
      <div className="role-banner__title">
        {info.emoji} You are the {info.label}
        {!alive && " (dead)"}
      </div>
      <div className="role-banner__description">{info.description}</div>
    </div>
  );
}
