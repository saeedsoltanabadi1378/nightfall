import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { telegram } from "../lib/telegram";

export type Language = "en" | "fa";

const translations = {
  en: {
    language: "فارسی",
    loading: "🌙 Loading Nightfall…",
    genericError: "Something went wrong.",
    noGame: "Open Nightfall from a game message in your Telegram group. Use /newgame, then tap Open Nightfall.",
    waitingLobby: "Waiting in the lobby — {count} joined.", joinLobby: "Join lobby", startGame: "Start game",
    needPlayers: "Need at least 5 players ({count}/5)", waitingCreatorStart: "Waiting for the game creator to start.",
    you: "you", dead: "dead", youAre: "You are the {role}",
    firstNight: "First Night", night: "Night {number}",
    firstNightInfo: "First night is for Mafia planning only. Detective, Doctor, and Mafia actions begin next night.",
    lastInvestigationMafia: "Last investigation: {name} is Mafia-aligned.",
    lastInvestigationTown: "Last investigation: {name} is not Mafia-aligned.", unknown: "unknown",
    deadWatch: "You are no longer able to act. Watch quietly.", noNightAction: "No night action for your role. Waiting for others…",
    chooseAction: "Choose someone to {action}:", actionSubmitted: "Action submitted. Waiting for the night to resolve…",
    endFirstNightHint: "End the discussion when the Mafia is ready.", endNightHint: "Night ends after every living night role submits an action.", endNight: "End night",
    investigate: "Investigate", heal: "Heal", kill: "Kill",
    day: "Day {number}", foundDead: "{name} was found dead this morning.", theyWere: " They were a {role}.",
    doctorSaved: "The Doctor saved the target! Nobody died last night.", nobodyDied: "Nobody died last night.",
    discuss: "Discuss, then move to voting when ready.", startVoting: "Start voting", waitingVoting: "Waiting for the game creator to start voting.",
    voting: "Voting", deadCannotVote: "You are no longer able to vote. Watch quietly.", votePrompt: "Vote to eliminate someone, or abstain:",
    vote: "Vote", abstain: "Abstain", voteSubmitted: "Vote submitted. Waiting for the rest of the town…", tallyVotes: "Tally votes",
    results: "Results", voteTie: "The vote was tied. Nobody was eliminated.", votedOut: "The town voted out {name}.",
    noVotes: "No votes were cast. Nobody was eliminated.", nextNight: "Continue to next night", waitingContinue: "Waiting for the game creator to continue.",
    gameOver: "Game Over", villagersWin: "The Villagers win!", mafiaWins: "The Mafia wins!",
    listenRoom: "🔇 Listen to room voice", joinRoom: "🎙️ Join room voice", joinMafia: "🔪 Join Mafia voice",
    inMafiaVoice: "🔊 In Mafia voice", inRoomVoice: "🔊 In room voice", listeningOnly: " (listening only)",
    mute: "🎤 Mute", unmute: "🔇 Unmute", leaveVoice: "Leave voice", voiceError: "Could not join voice chat. Check microphone permissions.",
    roleVillager: "Villager", roleDetective: "Detective", roleDoctor: "Doctor", roleMafia: "Mafia", roleGodfather: "Godfather",
    descVillager: "Work with the town to find the Mafia.", descDetective: "Each night, investigate one player to learn if they're Mafia-aligned.",
    descDoctor: "Each night, choose one player to protect from the Mafia's kill.", descMafia: "Work with your team to eliminate the Villagers without being caught.",
    descGodfather: "Leader of the Mafia. Choose who dies each night."
  },
  fa: {
    language: "English",
    loading: "🌙 در حال بارگذاری نایت‌فال…",
    genericError: "مشکلی پیش آمد.",
    noGame: "نایت‌فال را از پیام بازی در گروه تلگرام باز کنید. دستور /newgame را بزنید و سپس روی «باز کردن نایت‌فال» بزنید.",
    waitingLobby: "در انتظار بازیکنان — {count} نفر پیوسته‌اند.", joinLobby: "پیوستن به بازی", startGame: "شروع بازی",
    needPlayers: "حداقل ۵ بازیکن لازم است ({count}/۵)", waitingCreatorStart: "در انتظار شروع بازی توسط سازنده.",
    you: "شما", dead: "مرده", youAre: "نقش شما: {role}",
    firstNight: "شب اول", night: "شب {number}",
    firstNightInfo: "شب اول فقط برای هماهنگی مافیا است. عملکرد کارآگاه، دکتر و مافیا از شب بعد آغاز می‌شود.",
    lastInvestigationMafia: "نتیجه آخرین تحقیق: {name} عضو مافیا است.",
    lastInvestigationTown: "نتیجه آخرین تحقیق: {name} عضو مافیا نیست.", unknown: "نامشخص",
    deadWatch: "شما دیگر نمی‌توانید اقدامی انجام دهید. فقط تماشا کنید.", noNightAction: "نقش شما اقدام شبانه ندارد. منتظر دیگران باشید…",
    chooseAction: "یک نفر را برای «{action}» انتخاب کنید:", actionSubmitted: "اقدام ثبت شد. منتظر پایان شب باشید…",
    endFirstNightHint: "وقتی مافیا آماده بود، گفتگو را پایان دهید.", endNightHint: "پس از ثبت اقدام همه نقش‌های شب، شب پایان می‌یابد.", endNight: "پایان شب",
    investigate: "تحقیق", heal: "نجات", kill: "حذف",
    day: "روز {number}", foundDead: "{name} صبح امروز مرده پیدا شد.", theyWere: " نقش او {role} بود.",
    doctorSaved: "دکتر هدف را نجات داد! دیشب کسی کشته نشد.", nobodyDied: "دیشب کسی کشته نشد.",
    discuss: "گفتگو کنید و وقتی آماده بودید رأی‌گیری را شروع کنید.", startVoting: "شروع رأی‌گیری", waitingVoting: "در انتظار شروع رأی‌گیری توسط سازنده بازی.",
    voting: "رأی‌گیری", deadCannotVote: "شما دیگر نمی‌توانید رأی بدهید. فقط تماشا کنید.", votePrompt: "به یک نفر رأی دهید یا ممتنع باشید:",
    vote: "ثبت رأی", abstain: "رأی ممتنع", voteSubmitted: "رأی ثبت شد. منتظر سایر بازیکنان باشید…", tallyVotes: "شمارش آرا",
    results: "نتایج", voteTie: "رأی‌ها مساوی شد؛ کسی حذف نشد.", votedOut: "شهروندان {name} را حذف کردند.",
    noVotes: "رأیی ثبت نشد و کسی حذف نشد.", nextNight: "ادامه به شب بعد", waitingContinue: "در انتظار ادامه بازی توسط سازنده.",
    gameOver: "پایان بازی", villagersWin: "شهروندان برنده شدند!", mafiaWins: "مافیا برنده شد!",
    listenRoom: "🔇 شنیدن صدای اتاق", joinRoom: "🎙️ پیوستن به صدای اتاق", joinMafia: "🔪 پیوستن به صدای مافیا",
    inMafiaVoice: "🔊 در اتاق صوتی مافیا", inRoomVoice: "🔊 در اتاق صوتی عمومی", listeningOnly: " (فقط شنونده)",
    mute: "🎤 بی‌صدا", unmute: "🔇 فعال‌کردن صدا", leaveVoice: "خروج از صدا", voiceError: "اتصال صوتی ممکن نشد. دسترسی میکروفن را بررسی کنید.",
    roleVillager: "شهروند", roleDetective: "کارآگاه", roleDoctor: "دکتر", roleMafia: "مافیا", roleGodfather: "پدرخوانده",
    descVillager: "با شهروندان همکاری کنید تا مافیا را پیدا کنید.", descDetective: "هر شب درباره یک بازیکن تحقیق کنید تا وابستگی او به مافیا مشخص شود.",
    descDoctor: "هر شب یک بازیکن را از حمله مافیا نجات دهید.", descMafia: "با تیم خود شهروندان را بدون شناسایی‌شدن حذف کنید.",
    descGodfather: "رهبر مافیا؛ هر شب هدف حذف را انتخاب کنید."
  }
} as const;

type TranslationKey = keyof typeof translations.en;
type LanguageContextValue = { language: Language; setLanguage(language: Language): void; t(key: TranslationKey, values?: Record<string, string | number>): string };

const LanguageContext = createContext<LanguageContextValue | null>(null);

export function LanguageProvider({ children }: { children: ReactNode }) {
  const detected = telegram.initDataUnsafe.user?.language_code?.toLowerCase().startsWith("fa") ? "fa" : "en";
  const [language, setLanguage] = useState<Language>(() => (localStorage.getItem("nightfall-language") as Language | null) ?? detected);

  useEffect(() => {
    localStorage.setItem("nightfall-language", language);
    document.documentElement.lang = language;
    document.documentElement.dir = language === "fa" ? "rtl" : "ltr";
  }, [language]);

  const value = useMemo<LanguageContextValue>(() => ({
    language,
    setLanguage,
    t: (key, values) => Object.entries(values ?? {}).reduce<string>((text, [name, replacement]) => text.replaceAll(`{${name}}`, String(replacement)), translations[language][key]),
  }), [language]);

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

export function useLanguage() {
  const context = useContext(LanguageContext);
  if (!context) throw new Error("useLanguage must be used inside LanguageProvider");
  return context;
}
