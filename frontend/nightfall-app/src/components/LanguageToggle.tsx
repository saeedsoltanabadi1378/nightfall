import { useLanguage } from "../i18n/LanguageContext";

export function LanguageToggle() {
  const { language, setLanguage, t } = useLanguage();
  return (
    <button className="language-toggle" onClick={() => setLanguage(language === "en" ? "fa" : "en")}>
      {t("language")}
    </button>
  );
}
