import type { Page } from "@playwright/test";
import type { Fixture } from "./fixture";
import type { RoleName } from "./roles";

export type Project = "desktop" | "phone";

/**
 * One thing the stand looks at: who looks, where, and what is done on the page
 * before it is captured.
 *
 * Pages are reached the way a reader reaches them - by address, by the visible
 * name of a button or a link - and not by class, so a clean-up that renames a
 * class still reaches the same state, and the frames say what that clean-up
 * changed instead of failing to open the page.
 */
export interface State {
  name: string;
  role: RoleName;
  url: string | ((fixture: Fixture) => string);
  /** What is done before the capture. */
  act?: (page: Page) => Promise<void>;
  /**
   * Whether the window is stretched to the height of the page before the
   * capture, so the frame holds the whole page and not its first screen.
   * Off for the states of something that floats (a dialog, a menu, a picker):
   * those are about where they open in a window of the real size.
   */
  whole?: boolean;
  /** Only on these projects; both when omitted. */
  projects?: Project[];
  /** Entries of localStorage beyond the session and the theme. */
  storage?: Record<string, string>;
  /** The instant the page's clock stands at, when not the seed epoch. */
  clock?: (fixture: Fixture) => string;
}

const button = (page: Page, name: string) =>
  page.getByRole("button", { name, exact: true }).first();

/** A dead token: the letter pages read it from the fragment, and the server knows none such. */
const DEAD_TOKEN = "#token=0000dead0000dead0000dead0000dead";

/** The guest ticket of the seed, whose tracking token is fixed (DataSeeder.Moderation.cs). */
const GUEST_TICKET_TOKEN = "5eedc0de5eedc0de5eedc0de5eedc0de";

/** A finished game whose first post carries an attachment, two rolls and reviews (DataSeeder.Games.cs). */
const BIG_POST_GAME = "aaabp";

/** The game the member owns, and the account removed by the seed. */
const OWNED_GAME = "aaaam";
const REMOVED_ACCOUNT = "Ушедший Мастер";

/** Some markup typed into the comment editor of a topic, in its BBCode mode. */
async function writeBbcode(page: Page) {
  await button(page, "Режим BBCode").click();
  await page
    .getByRole("textbox", { name: "Написать комментарий..." })
    .fill("[b]Жирный[/b] и [i]курсив[/i]\n[quote]Цитата[/quote]");
}

async function openFilter(page: Page) {
  await button(page, "Фильтры").click();
}

/** On a phone the columns live in the sliding panel the menu button opens. */
async function openColumn(page: Page, tab: "Меню" | "О сайте") {
  await page.getByTestId("menu-button").click();
  await page.getByRole("tab", { name: tab, exact: true }).click();
}

export const STATES: State[] = [
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
    url: `/support/track/${GUEST_TICKET_TOKEN}`,
  },
  {
    name: "guest-columns-left",
    role: "guest",
    url: "/",
    whole: false,
    projects: ["phone"],
    act: (page) => openColumn(page, "Меню"),
  },
  {
    name: "guest-columns-right",
    role: "guest",
    url: "/",
    whole: false,
    projects: ["phone"],
    act: (page) => openColumn(page, "О сайте"),
  },

  // --- Member ------------------------------------------------------------
  { name: "member-home", role: "user", url: "/" },
  {
    name: "member-logout-menu",
    role: "user",
    url: "/",
    whole: false,
    projects: ["desktop"],
    act: async (page) => {
      await page.getByTestId("logout-button").click();
    },
  },
  {
    // The one state where the time zone question is left to be asked: the
    // browser's zone differs from the account's, and the toast says so.
    name: "member-toast-time-zone",
    role: "user",
    url: "/",
    whole: false,
    storage: { "dm3-time-zone-asked": "" },
    act: async (page) => {
      await button(page, "синхронизировать").waitFor();
    },
  },
  {
    name: "member-game-player",
    role: "user",
    url: (f) => `/game/${f.playedGame}`,
  },
  { name: "member-game-owner", role: "user", url: `/game/${OWNED_GAME}` },
  {
    name: "member-game-settings",
    role: "user",
    url: `/game/${OWNED_GAME}/settings`,
  },
  {
    name: "member-game-characters",
    role: "user",
    url: (f) => `/game/${f.playedGame}/characters`,
  },
  {
    name: "member-character-edit",
    role: "user",
    url: (f) => `/game/${f.playedGame}/characters/${f.characterId}/edit`,
  },
  {
    name: "member-room",
    role: "user",
    url: (f) => `/game/${f.playedGame}/rooms/1`,
  },
  {
    // The clock stands five minutes after the member's last post, inside the
    // window in which the interface lets him edit it.
    name: "member-room-own-post-edit",
    role: "user",
    url: (f) => `/game/${f.playedGame}/rooms/1`,
    clock: (f) =>
      new Date(Date.parse(f.ownPostCreatedUtc) + 5 * 60_000).toISOString(),
    act: async (page) => {
      await page
        .getByRole("button", { name: /^Редактировать/ })
        .last()
        .click();
    },
  },
  {
    name: "member-room-big-post",
    role: "user",
    url: `/game/${BIG_POST_GAME}/rooms/1?number=1`,
  },
  {
    name: "member-room-reviews-open",
    role: "user",
    url: `/game/${BIG_POST_GAME}/rooms/1?number=1`,
    act: async (page) => {
      await page
        .getByRole("button", { name: /^Оценки/ })
        .first()
        .click();
    },
  },
  {
    name: "member-room-chat",
    role: "user",
    url: (f) => `/game/${f.playedGame}/chat-rooms/${f.chatRoomNumber}`,
  },
  { name: "member-blog", role: "user", url: (f) => `/blogs/${f.blogId}` },
  {
    name: "member-blog-feed",
    role: "user",
    url: (f) => `/blogs/${f.blogId}/feed`,
  },
  {
    name: "member-blog-settings",
    role: "user",
    url: (f) => `/blogs/${f.blogId}/settings`,
  },
  {
    name: "member-global-chat-full",
    role: "user",
    url: "/global-chat",
    storage: { dm_message_layout: "full" },
  },
  {
    name: "member-global-chat-compact",
    role: "user",
    url: "/global-chat",
    storage: { dm_message_layout: "compact" },
  },
  { name: "member-messenger", role: "user", url: "/messenger" },
  {
    name: "member-messenger-chat-full",
    role: "user",
    url: (f) => `/messenger/c/${f.chatId}`,
    storage: { dm_message_layout: "full" },
  },
  {
    name: "member-messenger-chat-compact",
    role: "user",
    url: (f) => `/messenger/c/${f.chatId}`,
    storage: { dm_message_layout: "compact" },
  },
  { name: "member-notepad", role: "user", url: "/notepad" },
  { name: "member-notifications", role: "user", url: "/notifications" },
  { name: "member-subscriptions", role: "user", url: "/subscriptions" },
  { name: "member-my-tickets", role: "user", url: "/my-tickets" },
  { name: "member-profile-own", role: "user", url: "/users/TestUser" },
  {
    name: "member-profile-own-games",
    role: "user",
    url: "/users/TestUser/games",
  },
  { name: "member-profile-other", role: "user", url: "/users/SolohinLex" },
  {
    name: "member-profile-other-achievements",
    role: "user",
    url: "/users/SolohinLex/achievements",
  },
  {
    name: "member-profile-removed",
    role: "user",
    url: `/users/${encodeURIComponent(REMOVED_ACCOUNT)}`,
  },
  {
    name: "member-profile-given-reviews",
    role: "user",
    url: "/users/TestUser/given-reviews",
  },
  { name: "member-account", role: "user", url: "/account" },
  { name: "member-pulse", role: "user", url: "/pulse" },
  { name: "member-forum-topic", role: "user", url: "/forum/general/4" },
  {
    name: "member-editor-bbcode",
    role: "user",
    url: "/forum/general/4",
    act: writeBbcode,
  },
  {
    name: "member-editor-wysiwyg",
    role: "user",
    url: "/forum/general/4",
    act: async (page) => {
      await writeBbcode(page);
      await button(page, "Визуальный редактор").click();
    },
  },
  {
    name: "member-editor-insert-link",
    role: "user",
    url: "/forum/general/4",
    whole: false,
    act: async (page) => {
      await page
        .getByRole("button", { name: "Вставить ссылку" })
        .last()
        .click();
    },
  },
  {
    name: "member-editor-insert-image",
    role: "user",
    url: "/forum/general/4",
    whole: false,
    act: async (page) => {
      await page
        .getByRole("button", { name: "Вставить изображение" })
        .last()
        .click();
    },
  },
  {
    name: "member-editor-help",
    role: "user",
    url: "/forum/general/4",
    whole: false,
    act: async (page) => {
      await page
        .getByRole("button", { name: "Показать справку" })
        .last()
        .click();
    },
  },
  {
    // A picker opened at the right edge of the window: where it opens and
    // whether it stays inside is the point.
    name: "member-statistics-month-picker",
    role: "user",
    url: "/statistics?year=2026&month=6",
    whole: false,
    act: async (page) => {
      await page
        .getByRole("button", { name: /Июнь 2026/ })
        .first()
        .click();
    },
  },
  {
    name: "member-tooltip",
    role: "user",
    url: "/",
    whole: false,
    projects: ["desktop"],
    act: async (page) => {
      await button(page, "Прокрутить наверх").hover();
      await page.getByRole("tooltip").waitFor();
    },
  },
  {
    // The question before deleting the member's own game: it opens on the
    // click and nothing is written until it is answered, which the stand
    // never does. A state must not change the stand: "Выйти везде", the
    // first choice here, ended every session of the member and turned each
    // of his later states into a guest's.
    name: "member-confirm-dialog",
    role: "user",
    url: `/game/${OWNED_GAME}/settings`,
    whole: false,
    act: async (page) => {
      await button(page, "Удалить игру").click();
      await page.getByRole("alertdialog").waitFor();
    },
  },
  {
    name: "member-columns-left-game",
    role: "user",
    url: (f) => `/game/${f.playedGame}`,
    whole: false,
    projects: ["phone"],
    act: (page) => openColumn(page, "Меню"),
  },
  {
    name: "member-columns-right-game",
    role: "user",
    url: (f) => `/game/${f.playedGame}`,
    whole: false,
    projects: ["phone"],
    act: (page) => openColumn(page, "О сайте"),
  },

  // --- Staff -------------------------------------------------------------
  { name: "mentor-home", role: "mentor", url: "/" },
  { name: "mentor-game", role: "mentor", url: "/game/aaaad" },
  { name: "moderator-home", role: "moderator", url: "/" },
  { name: "moderator-moderation", role: "moderator", url: "/moderation" },
  { name: "moderator-bans", role: "moderator", url: "/moderation/bans" },
  {
    name: "moderator-warnings",
    role: "moderator",
    url: "/moderation/warnings",
  },
  { name: "moderator-tickets", role: "moderator", url: "/moderation/tickets" },
  {
    name: "moderator-ticket",
    role: "moderator",
    url: (f) => `/moderation/tickets/${f.ticketId}`,
  },
  {
    name: "moderator-profile",
    role: "moderator",
    url: "/users/RatingNegative",
  },
  { name: "senior-home", role: "senior", url: "/" },
  {
    name: "senior-username-changes",
    role: "senior",
    url: "/moderation/username-changes",
  },
  { name: "senior-violators", role: "senior", url: "/moderation/violators" },
  { name: "admin-home", role: "admin", url: "/" },
  { name: "admin-moderation", role: "admin", url: "/moderation" },
  { name: "admin-tags", role: "admin", url: "/moderation/tags" },
  {
    name: "admin-removed-accounts",
    role: "admin",
    url: "/moderation/removed-accounts",
  },
  {
    name: "admin-profile-moderation",
    role: "admin",
    url: "/users/RatingNegative",
  },
];

/** Nodes every state records the computed style of, found by tag and landmark. */
export const COMMON_KEYS = [
  "body",
  "header",
  "header a",
  "header button",
  "nav a",
  "aside h2",
  "aside a",
  "main",
  "main h1",
  "main h2",
  "main p",
  "main a",
  "main button",
  "main input",
  "main textarea",
  "main table",
  "main th",
  "main td",
  "main li",
  "footer",
  "footer a",
  "[role=dialog]",
  "[role=dialog] h2",
  "[role=dialog] button",
  "[role=dialog] input",
  "[role=alertdialog]",
  "[role=alertdialog] button",
  "[role=menu]",
  "[role=tooltip]",
  "[role=status]",
  "[role=alert]",
];
