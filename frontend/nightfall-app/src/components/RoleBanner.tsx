import type { Role } from "../api/types";
import { roleEmoji } from "../lib/gameText";
import { useLanguage } from "../i18n/LanguageContext";

export function RoleBanner({ role, alive }: { role: Role | null; alive: boolean }) {
  const { t } = useLanguage();
  if (!role) return null;
  const label = t(`role${role}` as "roleVillager");
  const description = t(`desc${role}` as "descVillager");

  return (
    <div className={`role-banner${alive ? "" : " role-banner--dead"}`}>
      <div className="role-banner__title">
        {roleEmoji[role]} {t("youAre", { role: label })}
        {!alive && ` (${t("dead")})`}
      </div>
      <div className="role-banner__description">{description}</div>
    </div>
  );
}
