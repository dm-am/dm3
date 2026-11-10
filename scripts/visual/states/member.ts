import {
  BIG_POST_GAME,
  button,
  OWNED_GAME,
  REMOVED_ACCOUNT,
  type State,
  writeBbcode,
} from "./shared";

export const MEMBER_STATES: State[] = [
  // --- Member ------------------------------------------------------------
  { name: "member-home", role: "user", url: "/" },
  {
    name: "member-logout-menu",
    role: "user",
    url: "/",
    whole: false,
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
];
