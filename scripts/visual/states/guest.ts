import {
  DEAD_TOKEN,
  GUEST_TICKET_TOKEN,
  openFilter,
  type State,
} from "./shared";

export const GUEST_STATES: State[] = [
  // --- Guest -------------------------------------------------------------
  { name: "guest-home", role: "guest", url: "/" },
  { name: "guest-games", role: "guest", url: "/games" },
  {
    name: "guest-games-filter-root",
    role: "guest",
    url: "/games",
    whole: false,
    act: openFilter,
  },
  {
    name: "guest-games-filter-level",
    role: "guest",
    url: "/games",
    whole: false,
    act: async (page) => {
      await openFilter(page);
      await page.getByRole("button", { name: /^Статус игры/ }).click();
      await page.getByRole("button", { name: /^Идет игра/ }).click();
    },
  },
  {
    name: "guest-games-filter-dates",
    role: "guest",
    url: "/games",
    whole: false,
    act: async (page) => {
      await openFilter(page);
      await page.getByRole("button", { name: /^Даты/ }).click();
    },
  },
  {
    name: "guest-games-bubbles",
    role: "guest",
    url: "/games?status=Active&recruitmentFilter=open&requiredTags=43,49&excludedTags=50&hosts=SolohinLex&createdFromUtc=2026-01-01",
  },
  { name: "guest-blogs", role: "guest", url: "/blogs" },
  { name: "guest-community", role: "guest", url: "/community" },
  { name: "guest-forum", role: "guest", url: "/forum" },
  { name: "guest-forum-board", role: "guest", url: "/forum/general" },
  { name: "guest-forum-topic", role: "guest", url: "/forum/general/4" },
  { name: "guest-game", role: "guest", url: "/game/aaaaa" },
  { name: "guest-room", role: "guest", url: "/game/aaaaa/rooms/1?number=1" },
  { name: "guest-profile", role: "guest", url: "/users/SolohinLex" },
  {
    name: "guest-statistics",
    role: "guest",
    url: "/statistics?year=2026&month=6",
  },
  { name: "guest-about", role: "guest", url: "/about" },
  { name: "guest-rules", role: "guest", url: "/rules" },
  { name: "guest-polls", role: "guest", url: "/polls" },
  { name: "guest-not-found", role: "guest", url: "/no-such-page" },
  { name: "guest-login", role: "guest", url: "/?action=login", whole: false },
  {
    name: "guest-registration",
    role: "guest",
    url: "/?action=register",
    whole: false,
  },
  {
    name: "guest-recovery",
    role: "guest",
    url: "/?action=recovery",
    whole: false,
  },
  {
    name: "guest-two-factor-removal",
    role: "guest",
    url: "/?action=two-factor",
    whole: false,
  },
  { name: "letter-activation", role: "guest", url: `/activate${DEAD_TOKEN}` },
  {
    name: "letter-email-change",
    role: "guest",
    url: `/confirm-email${DEAD_TOKEN}`,
  },
  {
    name: "letter-password-reset",
    role: "guest",
    url: `/reset-password${DEAD_TOKEN}`,
  },
  {
    name: "letter-username-change",
    role: "guest",
    url: `/change-username${DEAD_TOKEN}`,
  },
  {
    name: "letter-two-factor-removal",
    role: "guest",
    url: `/two-factor/removal${DEAD_TOKEN}`,
  },
  {
    name: "letter-two-factor-removal-cancel",
    role: "guest",
    url: `/two-factor/removal/cancel${DEAD_TOKEN}`,
  },
  { name: "guest-support", role: "guest", url: "/support" },
  { name: "guest-complaint", role: "guest", url: "/complaint" },
  {
    name: "guest-ticket-track",
    role: "guest",
    url: `/support/track#token=${GUEST_TICKET_TOKEN}`,
  },
];
