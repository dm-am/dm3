import type { Page } from "@playwright/test";
import type { Fixture } from "./fixture";
import type { RoleName } from "./roles";

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
  /** Entries of localStorage beyond the session and the theme. */
  storage?: Record<string, string>;
  /** The instant the page's clock stands at, when not the seed epoch. */
  clock?: (fixture: Fixture) => string;
}

/** A button by its whole visible name, or by a pattern of it. */
const button = (page: Page, name: string | RegExp) =>
  page
    .getByRole("button", {
      name,
      exact: typeof name === "string" ? true : undefined,
    })
    .first();

/** A token the server knows none such. */
const DEAD = "0000dead0000dead0000dead0000dead";

/** The dead token where the letter pages read it: in the fragment. */
const DEAD_TOKEN = `#token=${DEAD}`;

/** The guest ticket of the seed, whose tracking token is fixed (DataSeeder.Tickets.cs). */
const GUEST_TICKET_TOKEN = "5eedc0de5eedc0de5eedc0de5eedc0de";

/** A finished game whose first post carries an attachment, two rolls and reviews (DataSeeder.GamePosts.cs, the reviews DataSeeder.Showcase.cs). */
const BIG_POST_GAME = "aaabp";

/**
 * The finished game whose figure's note carries a map marked for Бран Молчун and a
 * letter for every reader (DataSeeder.HiddenGameContent.cs); no role plays in it.
 */
const HIDDEN_GAME = "aaadb";

/** The game the member owns, and the account removed by the seed. */
const OWNED_GAME = "aaaam";
const REMOVED_ACCOUNT = "Ушедший Мастер";

/** The game the administrator masters: pendency, five rooms, a declined character (DataSeeder.Games.cs). */
const ADMIN_GAME = "aaaaa";

/** Its declined character, by the id the seed fixes, since no list shows one (DataSeeder.Characters.cs). */
const DECLINED_CHARACTER = "00000000-0000-0000-00c1-000000000001";

/** A public draft of Experienced without characters, whose sheet saves straight away. */
const DRAFT_GAME = "aaabh";

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

/**
 * Answers one method on one address the way the API refuses, with a problem
 * document whose title is the sentence the page shows, and lets every other
 * request through. The refusal never reaches the server, so a state that
 * shows how a page takes a refusal writes nothing. Addresses are regular
 * expressions: a glob reads the "?" of a query string as a wildcard.
 */
async function refuse(
  page: Page,
  url: RegExp,
  method: string,
  status: number,
  title: string,
) {
  await page.route(url, (route) =>
    route.request().method() === method
      ? route.fulfill({
          status,
          contentType: "application/problem+json",
          body: JSON.stringify({ type: "about:blank", title, status }),
        })
      : route.fallback(),
  );
}

/** Answers GET on one address with the given body, for a list the seed cannot hold for long. */
async function answer(page: Page, url: RegExp, body: unknown) {
  await page.route(url, (route) =>
    route.request().method() === "GET"
      ? route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify(body),
        })
      : route.fallback(),
  );
}

/**
 * Hovers the matching elements one after another until a tooltip with the
 * text opens: which of them has one is a draw of the seed.
 */
async function hoverUntilTooltip(page: Page, selector: string, text: string) {
  const targets = page.locator(selector);
  const count = await targets.count();
  for (let i = 0; i < count; i++) {
    await targets.nth(i).hover();
    try {
      await page
        .getByRole("tooltip")
        .filter({ hasText: text })
        .waitFor({ timeout: 800 });
      return;
    } catch {
      // Not this one: the next.
    }
  }
  throw new Error(`no tooltip with "${text}" over ${count} of ${selector}`);
}

/** A like the reader may press with a count above zero, by its words "Нравится (3)". */
const LIKED = 'button:text-matches("^Нравится [(][0-9]+[)]$")';

/** A section of the settings of a game or a blog, by its heading. */
const settingsSection = (page: Page, title: string) =>
  page.locator("section.settings-section", { hasText: title }).first();

/** A section of the account page, by its heading. */
const accountSection = (page: Page, title: string) =>
  page
    .locator("section", {
      has: page.getByRole("heading", { name: title, exact: true }),
    })
    .first();

/**
 * Fills the login dialog with an address and the seed's password, sends it and
 * waits for the answer: the form holds the request back until three seconds
 * after it opened (useBotTrap), so the page is quiet before it goes out.
 */
async function signIn(page: Page, email: string) {
  await page.getByRole("textbox", { name: /почт/i }).fill(email);
  await page.locator('input[type="password"]').fill("Test123!");
  const answered = page.waitForResponse((response) =>
    /\/v1\/account\/login$/.test(response.url()),
  );
  // The dialog's own button: the header has one of the same name.
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Войти", exact: true })
    .click();
  await answered;
}

/** A picture of one pixel: enough to be a file the composer picked. */
const PNG_1X1 = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
  "base64",
);

/** "карта.png" chosen in the form of a post: a file the form holds, never sent. */
async function chooseComposerFile(page: Page) {
  await page.locator(".attachments input[type=file]").setInputFiles({
    name: "карта.png",
    mimeType: "image/png",
    buffer: PNG_1X1,
  });
}

/** A moment some minutes past another, for the clock of a state. */
const minutesAfter = (moment: string, minutes: number) =>
  new Date(Date.parse(moment) + minutes * 60_000).toISOString();

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
    url: `/support/track#token=${GUEST_TICKET_TOKEN}`,
  },

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

  // --- One place each ------------------------------------------------------
  // A refusal, a menu, a hint, an edge of the data: each state below opens one
  // place of a page that the states above pass by. They come after those on
  // purpose: the run goes in the file's order, and a page one of them opens is
  // read for the states that follow, so the states above see what they saw.

  // Shell
  {
    name: "guest-home-sidebar-failed",
    role: "guest",
    url: "/",
    act: async (page) => {
      await refuse(page, /\/v1\/polls\?.*status=Active/, "GET", 500, "Ошибка");
      await refuse(page, /\/v1\/games\/tags/, "GET", 500, "Ошибка");
      await page.reload({ waitUntil: "load" });
    },
  },
  {
    name: "guest-home-select-all",
    role: "guest",
    url: "/",
    act: async (page) => {
      await page.locator("body").click({ position: { x: 5, y: 5 } });
      await page.keyboard.press("Control+A");
    },
  },
  {
    // Five presses of Tab from the top of the page: the ring of the focus.
    name: "guest-home-tab-focus",
    role: "guest",
    url: "/",
    act: async (page) => {
      for (let i = 0; i < 5; i++) await page.keyboard.press("Tab");
    },
  },

  // Games
  {
    name: "guest-games-filter-status",
    role: "guest",
    url: "/games",
    whole: false,
    act: async (page) => {
      await openFilter(page);
      await button(page, /^Статус игры/).click();
    },
  },
  {
    name: "guest-games-filter-tags",
    role: "guest",
    url: "/games",
    whole: false,
    act: async (page) => {
      await openFilter(page);
      await button(page, /^Тег/).click();
      await page.getByRole("textbox").last().fill("яяяя");
    },
  },
  {
    name: "guest-games-bad-tag",
    role: "guest",
    url: "/games?requiredTags=abc",
  },
  {
    // The setting of this game starts with a Cyrillic letter among Latin ones.
    name: "guest-game-crazy-adventures",
    role: "guest",
    url: "/games",
    act: async (page) => {
      await page
        .getByPlaceholder("Поиск по названию")
        .fill("Безумные Приключения");
      await page
        .getByRole("link", { name: "Безумные Приключения" })
        .first()
        .click();
      await page.waitForURL(/\/game\/[^/]+$/);
    },
  },
  {
    // The two counters whose tooltip counts what an account still sets up.
    name: "guest-community-tooltip-games",
    role: "guest",
    url: "/community",
    whole: false,
    act: (page) =>
      hoverUntilTooltip(
        page,
        'a[aria-label^="Игр в роли ведущего:"]',
        "Оформляется:",
      ),
  },
  {
    name: "guest-community-tooltip-blogs",
    role: "guest",
    url: "/community",
    whole: false,
    act: (page) =>
      hoverUntilTooltip(page, 'a[aria-label^="Блогов:"]', "Оформляется:"),
  },
  // Games and blogs of the administrator in the statuses his first ones are not in.
  {
    name: "admin-game-frozen",
    role: "admin",
    url: "/game/aaacg",
  },
  {
    name: "admin-game-finished",
    role: "admin",
    url: "/game/aaabt",
  },
  {
    name: "admin-blog-active",
    role: "admin",
    url: "/blogs/aaaaa",
  },
  {
    name: "admin-blog-closed",
    role: "admin",
    url: "/blogs/aaaaq",
  },
  {
    // The parameters of the address under the names they had before.
    name: "member-pulse-address-params",
    role: "user",
    url: "/pulse?q=Диопсид&sort=rating&order=asc&minRating=1&createdFrom=2026-01-01",
  },
  {
    name: "member-room-address-params",
    role: "user",
    url: `/game/${BIG_POST_GAME}/rooms/1?number=1&q=Диопсид&sort=rating&order=desc&minRating=1`,
  },
  {
    name: "member-pulse-sort-menu",
    role: "user",
    url: "/pulse",
    whole: false,
    act: async (page) => {
      await button(page, /^Сортировка/).click();
    },
  },
  {
    name: "member-pulse-two-menus",
    role: "user",
    url: "/pulse",
    whole: false,
    act: async (page) => {
      await button(page, /^Сортировка/).click();
      await openFilter(page);
    },
  },
  {
    name: "member-room-sort-menu",
    role: "user",
    url: `/game/${BIG_POST_GAME}/rooms/1?number=1`,
    whole: false,
    act: async (page) => {
      await button(page, /^Сортировка/).click();
    },
  },
  {
    name: "member-create-game",
    role: "user",
    url: "/games/create",
  },
  {
    // A game of TestSeniorMod with one review: the member may write his own.
    name: "member-game-reviews",
    role: "user",
    url: "/game/aaaab/reviews",
  },
  {
    name: "guest-game-reviews",
    role: "guest",
    url: `/game/${ADMIN_GAME}/reviews`,
  },
  {
    name: "guest-game-reviews-garbage",
    role: "guest",
    url: `/game/${ADMIN_GAME}/reviews?number=abc`,
  },
  {
    name: "admin-game-schema-conflict",
    role: "admin",
    url: `/game/${DRAFT_GAME}/settings`,
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/games\/[^/]+\/attribute-schema/,
        "PUT",
        409,
        "Конфликт",
      );
      const section = settingsSection(page, "Схема атрибутов");
      await section.getByRole("button", { name: "+ Новый атрибут" }).click();
      await page.locator("#attr-title").fill("Тест");
      await section.getByRole("button", { name: "Готово" }).click();
      await section
        .getByRole("button", { name: "Сохранить", exact: true })
        .click();
    },
  },
  {
    // An attribute without a title goes to the server, which refuses it
    // before it writes (AttributeSpecificationRules): the refusal is its own.
    name: "admin-game-schema-empty-title",
    role: "admin",
    url: `/game/${DRAFT_GAME}/settings`,
    act: async (page) => {
      const section = settingsSection(page, "Схема атрибутов");
      await section.getByRole("button", { name: "+ Новый атрибут" }).click();
      await section.getByRole("button", { name: "Готово" }).click();
      await section
        .getByRole("button", { name: "Сохранить", exact: true })
        .click();
    },
  },
  {
    // 150 letters in the name of a new room, which the server caps at 100;
    // "Создать" is not pressed.
    name: "member-game-settings-room-title-long",
    role: "user",
    url: `/game/${OWNED_GAME}/settings`,
    act: async (page) => {
      await page.locator("#new-room-title").fill("а".repeat(150));
    },
  },
  {
    name: "admin-room-pendency-hover",
    role: "admin",
    url: `/game/${ADMIN_GAME}/rooms/1`,
    act: async (page) => {
      await button(page, "Снять ожидание хода").hover();
    },
  },
  {
    // A hidden roll is only prepared in the form: it goes out with the post,
    // which is never sent.
    name: "member-room-composer-roll",
    role: "user",
    url: (f) => `/game/${f.playedGame}/rooms/1`,
    act: async (page) => {
      await page.getByLabel("Показывать всем результаты бросков").uncheck();
      await button(page, "Бросить D20").click();
    },
  },
  {
    name: "member-room-composer-attachment",
    role: "user",
    url: (f) => `/game/${f.playedGame}/rooms/1`,
    act: chooseComposerFile,
  },
  {
    // "видят все" of the file of the form opened: the panel "Кто видит
    // карта.png", the first character checked and not yet taken by "Готово";
    // the post is never sent, so the file goes nowhere.
    name: "member-room-composer-attachment-selected",
    role: "user",
    url: (f) => `/game/${f.playedGame}/rooms/1`,
    act: async (page) => {
      await chooseComposerFile(page);
      await button(page, "видят все").click();
      await page
        .getByRole("dialog", { name: "Кто видит карта.png" })
        .getByRole("checkbox")
        .first()
        .check();
    },
  },
  {
    // The same file after "Готово": its name green, "видят: <имя>" in its
    // line, the readers in the tooltip over the name. Nothing is sent.
    name: "member-room-composer-attachment-chosen",
    role: "user",
    url: (f) => `/game/${f.playedGame}/rooms/1`,
    act: async (page) => {
      await chooseComposerFile(page);
      await button(page, "видят все").click();
      const panel = page.getByRole("dialog", { name: "Кто видит карта.png" });
      await panel.getByRole("checkbox").first().check();
      await panel.getByRole("button", { name: "Готово" }).click();
      await page.locator(".attachments .attachment-name").hover();
      await page
        .getByRole("tooltip")
        .filter({ hasText: "Получатели" })
        .waitFor();
    },
  },
  {
    // The figure's note to a reader the map is not for: the letter alone under
    // the clip, no count beside it, no trace of the map.
    name: "guest-room-hidden-file",
    role: "guest",
    url: `/game/${HIDDEN_GAME}/rooms/1`,
    act: async (page) => {
      await page
        .getByRole("button", { name: "Вложение", exact: true })
        .last()
        .click();
    },
  },
  {
    // The file of the big post as a reader it is kept for gets it: green, its
    // readers in the tooltip. No role reads a marked file in the seed, so the
    // GET of the posts is answered with the file marked; nothing is written.
    name: "member-room-marked-file",
    role: "user",
    url: `/game/${BIG_POST_GAME}/rooms/1?number=1`,
    act: async (page) => {
      await page.route(/\/v1\/rooms\/[^/]+\/posts/, async (route) => {
        if (route.request().method() !== "GET") return route.fallback();
        const response = await route.fetch();
        const json = await response.json();
        const marked = json.resources?.find(
          (post: { attachments?: unknown[] }) => post.attachments?.length,
        );
        if (marked)
          Object.assign(marked.attachments[0], {
            recipients: "DungeonMaster, Assistant, Вася",
            addressees: [],
          });
        await route.fulfill({ response, json });
      });
      await page.reload({ waitUntil: "load" });
      const toggle = button(page, /^Вложени/);
      await toggle.click();
      // The strip opens by a transition: hovered while it moves, the name
      // slides from under the pointer and the tooltip comes late or never.
      // So the hover waits for the strip to be open and still.
      const stripId = await toggle.getAttribute("aria-controls");
      await page
        .locator(`[aria-controls="${stripId}"][aria-expanded="true"]`)
        .waitFor();
      const strip = page.locator(`#${stripId}`);
      await strip.evaluate((list) =>
        Promise.all(
          (list.closest(".expand-fold") ?? list)
            .getAnimations({ subtree: true })
            .map((animation) => animation.finished),
        ),
      );
      await strip.getByRole("link").first().hover();
      await page
        .getByRole("tooltip")
        .filter({ hasText: "Получатели" })
        .waitFor();
    },
  },
  {
    // The room's own error block, with its "Повторить".
    name: "member-room-chat-load-failed",
    role: "user",
    url: (f) => `/game/${f.playedGame}/chat-rooms/${f.chatRoomNumber}`,
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/chat-rooms\/[^/]+\/messages/,
        "GET",
        500,
        "Ошибка",
      );
      await page.reload({ waitUntil: "load" });
    },
  },

  // Characters
  {
    name: "member-game-character",
    role: "user",
    url: (f) => `/game/${f.playedGame}/characters/${f.characterId}`,
  },
  {
    name: "member-game-character-failed",
    role: "user",
    url: (f) => `/game/${f.playedGame}/characters/${f.characterId}`,
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/characters\/[0-9a-f-]{36}$/,
        "GET",
        500,
        "Ошибка",
      );
      await page.reload({ waitUntil: "load" });
    },
  },
  {
    name: "guest-game-characters",
    role: "guest",
    url: `/game/${ADMIN_GAME}/characters`,
  },
  {
    name: "guest-game-character-declined",
    role: "guest",
    url: `/game/${ADMIN_GAME}/characters/${DECLINED_CHARACTER}`,
  },
  {
    // The master reads both notepads of a character of his game.
    name: "admin-game-character",
    role: "admin",
    url: (f) => `/game/${ADMIN_GAME}/characters/${f.uiCharacterId}`,
  },
  {
    name: "member-owned-game-characters",
    role: "user",
    url: `/game/${OWNED_GAME}/characters`,
  },
  {
    name: "member-owned-game-npc",
    role: "user",
    url: (f) => `/game/${OWNED_GAME}/characters/${f.npcId}`,
  },

  // Blogs and forum
  {
    name: "guest-blog-rubric-hover",
    role: "guest",
    url: "/blogs/aaaaa",
    whole: false,
    act: async (page) => {
      await page.getByRole("link", { name: "Для избранных" }).first().hover();
    },
  },
  {
    name: "member-blog-other",
    role: "user",
    url: "/blogs/aaaaa",
  },
  {
    // The invitations of the seed are swept a week after it was laid out, and
    // a stand seeded on the pinned epoch has them swept at once: the list is
    // answered here, in the shape the server gives (no role of the invited).
    name: "member-blog-settings-invitations",
    role: "user",
    url: (f) => `/blogs/${f.blogId}/settings`,
    act: async (page) => {
      const invited = (n: number, username: string) => ({
        id: `00000000-0000-0000-0000-0000000000b${n}`,
        blogId: "00000000-0000-0000-0000-0000000000b0",
        blogTitle: "Блог для кадра",
        invitedUser: {
          id: `00000000-0000-0000-0000-0000000000c${n}`,
          username,
          lastActivityUtc: "2026-01-01T00:00:00Z",
        },
        inviterUsername: "TestUser",
        type: "reader",
        createdUtc: "2026-01-01T00:00:00Z",
        expiresUtc: "2026-02-01T00:00:00Z",
      });
      await answer(page, /\/v1\/blogs\/[^/]+\/invitations$/, {
        resources: [invited(1, "SolohinLex"), invited(2, "TestMentor")],
        paging: null,
      });
      await page.reload({ waitUntil: "load" });
    },
  },
  {
    name: "member-forum-index",
    role: "user",
    url: "/forum",
  },
  {
    // TestModerator moderates the board: the pinned topics carry the handles.
    name: "moderator-forum-board",
    role: "moderator",
    url: "/forum/general",
  },
  {
    name: "guest-forum-news",
    role: "guest",
    url: "/forum/news",
  },
  {
    // Opened by its title: the number of the topic follows the digests the API
    // writes when it starts.
    name: "guest-forum-news-topic",
    role: "guest",
    url: "/forum/news",
    act: async (page) => {
      await page
        .getByRole("link", { name: "Запуск новой версии Dungeon Master!" })
        .click();
    },
  },
  {
    // The clock five minutes after the member's comment, inside the window in
    // which he may edit it, and then past that window.
    name: "member-forum-comment-own",
    role: "user",
    url: (f) => f.ownCommentTopicUrl,
    clock: (f) => minutesAfter(f.ownCommentCreatedUtc, 5),
  },
  {
    name: "member-forum-comment-own-late",
    role: "user",
    url: (f) => f.ownCommentTopicUrl,
    clock: (f) => minutesAfter(f.ownCommentCreatedUtc, 20),
  },
  {
    // The search over the comments applies when its box loses the focus.
    name: "member-forum-comments-wildcard",
    role: "user",
    url: "/forum/general/4",
    act: async (page) => {
      const search = page.getByPlaceholder("Поиск по тексту");
      await search.fill("%");
      await search.blur();
    },
  },
  {
    // Likes of the seed are drawn, so the first count above zero is hovered:
    // its tooltip names who likes it.
    name: "member-forum-like-tooltip",
    role: "user",
    url: "/forum/general/4",
    whole: false,
    act: (page) => hoverUntilTooltip(page, LIKED, "Нравится:"),
  },
  {
    name: "member-forum-comment-refused",
    role: "user",
    url: "/forum/general/4",
    act: async (page) => {
      // An empty title: the page falls back to a sentence of its own.
      await refuse(page, /\/v1\/topics\/[^/]+\/comments$/, "POST", 500, "");
      await button(page, "Режим BBCode").click();
      await page
        .getByRole("textbox", { name: "Написать комментарий..." })
        .fill("тест");
      await button(page, "Отправить").click();
    },
  },
  {
    name: "moderator-comment-warn-dialog",
    role: "moderator",
    url: "/forum/general/4",
    whole: false,
    act: async (page) => {
      await page.getByText("Предупреждение", { exact: true }).first().click();
      await page.getByRole("dialog").waitFor();
    },
  },

  // Chat
  {
    name: "member-messenger-search",
    role: "user",
    url: "/messenger",
    whole: false,
    act: async (page) => {
      await page
        .getByRole("combobox", { name: "Поиск собеседника" })
        .fill("Pla");
      await page.locator(".autocomplete-item").first().waitFor();
    },
  },
  {
    // The whole page: in a window of the real size the composer, focused
    // after the load in some runs only, scrolls the page under the tooltip.
    name: "member-global-chat-like-tooltip",
    role: "user",
    url: "/global-chat",
    storage: { dm_message_layout: "full" },
    act: (page) => hoverUntilTooltip(page, LIKED, "Нравится:"),
  },
  {
    name: "member-global-chat-search-empty",
    role: "user",
    url: "/global-chat",
    whole: false,
    act: async (page) => {
      await page.getByPlaceholder(/Поиск/).first().fill("яяяя");
      await page.getByText("Ничего не найдено").waitFor();
    },
  },

  // Account
  {
    // Another number of rows on a page, and the save refused.
    name: "member-account-settings-refused",
    role: "user",
    url: "/account",
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/users\/me\/preferences/,
        "PATCH",
        400,
        "Тестовый отказ",
      );
      const pageSize = page.locator("#account-page-size");
      await pageSize.selectOption(
        (await pageSize.inputValue()) === "20" ? "30" : "20",
      );
      await accountSection(page, "Личные настройки")
        .getByRole("button", { name: "Сохранить" })
        .click();
    },
  },
  {
    // The own settings changed and not saved: another number of rows on a
    // page, "Сохранить" open. Nothing is sent.
    name: "member-account-settings-changed",
    role: "user",
    url: "/account",
    act: async (page) => {
      const section = accountSection(page, "Личные настройки");
      const pageSize = section.locator("#account-page-size");
      await pageSize.selectOption(
        (await pageSize.inputValue()) === "20" ? "30" : "20",
      );
    },
  },
  {
    name: "member-account-password-refused",
    role: "user",
    url: "/account",
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/account\/password/,
        "POST",
        400,
        "Тестовый отказ",
      );
      await page.locator("#old-password").fill("Test123!");
      await page.locator("#new-password").fill("Another123!");
      await page.locator("#confirm-password").fill("Another123!");
      await button(page, "Изменить пароль").click();
    },
  },
  {
    // The new password typed twice and the current one left empty: the
    // button stays off.
    name: "member-account-password-empty",
    role: "user",
    url: "/account",
    act: async (page) => {
      await page.locator("#new-password").fill("Another123!");
      await page.locator("#confirm-password").fill("Another123!");
    },
  },
  {
    // The sessions of the stand's own sign-ins are folded into one, so the
    // list is answered with one more device; its "Выйти со всех других
    // устройств" is refused. "Выйти везде" is never pressed.
    name: "member-account-sessions-refused",
    role: "user",
    url: "/account",
    act: async (page) => {
      const session = (n: number, isCurrent: boolean) => ({
        id: `00000000-0000-0000-0000-0000000000e${n}`,
        createdUtc: "2026-06-01T10:00:00Z",
        expirationUtc: "2026-07-01T10:00:00Z",
        deviceInfo: isCurrent ? "Chrome, Windows" : "Firefox, Linux",
        ipAddress: isCurrent ? "127.0.0.1" : "10.0.0.2",
        isCurrent,
        persistent: true,
      });
      await answer(page, /\/v1\/account\/sessions$/, {
        resources: [session(1, true), session(2, false)],
        paging: null,
      });
      await refuse(
        page,
        /\/v1\/account\/sessions\/others/,
        "DELETE",
        400,
        "Тестовый отказ",
      );
      await page.reload({ waitUntil: "load" });
      await button(page, "Выйти со всех других устройств").click();
    },
  },
  {
    // The own name typed into the field: the field never offers it, so the
    // button stays off and the server's refusal is not reached from here.
    name: "member-account-blacklist-self",
    role: "user",
    url: "/account",
    act: async (page) => {
      await accountSection(page, "Черный список")
        .getByPlaceholder("Имя пользователя")
        .fill("TestUser");
    },
  },
  {
    // A reason shorter than the form takes: the button stays off.
    name: "member-account-username-short-reason",
    role: "user",
    url: "/account",
    act: async (page) => {
      await page.locator("#username-reason").fill("коротко");
    },
  },
  {
    // The invitations of the seed are swept a week after it was laid out:
    // the list is answered here, and so is the server's refusal to accept.
    name: "member-account-invitation-refused",
    role: "user",
    url: "/account",
    act: async (page) => {
      await answer(page, /\/v1\/users\/me\/invitations$/, {
        resources: [
          {
            id: "00000000-0000-0000-0000-0000000000a1",
            entityId: "00000000-0000-0000-0000-0000000000a2",
            entityType: "game",
            entityTitle: "Игра для кадра",
            inviterUsername: "SolohinLex",
            type: "player",
            createdUtc: "2026-01-01T00:00:00Z",
          },
        ],
        paging: null,
      });
      await refuse(
        page,
        /\/v1\/users\/me\/invitations\/[^/]+\/accept$/,
        "POST",
        404,
        "Invitation not found or already processed",
      );
      await page.reload({ waitUntil: "load" });
      await accountSection(page, "Приглашения")
        .getByRole("button", { name: "Принять" })
        .click();
    },
  },
  {
    // The pending registration of the seed; the letter is not sent again.
    name: "guest-login-pending-account",
    role: "guest",
    url: "/?action=login",
    whole: false,
    act: async (page) => {
      await signIn(page, "inactive@test.local");
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "guest-login-unknown-email",
    role: "guest",
    url: "/?action=login",
    whole: false,
    act: (page) => signIn(page, "nobody@test.local"),
  },
  {
    // LongestLoginPossible holds a permanent full ban (DataSeeder.Bans.cs).
    name: "guest-login-banned",
    role: "guest",
    url: "/?action=login",
    whole: false,
    act: (page) => signIn(page, "longest@test.local"),
  },
  {
    // The fields left empty, one after another: the button stays off.
    name: "guest-login-empty",
    role: "guest",
    url: "/?action=login",
    whole: false,
    act: async (page) => {
      await page.locator("#email").click();
      await page.keyboard.press("Tab");
      await page.locator("#password").click();
      await page.keyboard.press("Tab");
    },
  },
  {
    // The address left empty: the button stays off.
    name: "guest-registration-empty",
    role: "guest",
    url: "/?action=register",
    whole: false,
    act: async (page) => {
      await page.locator("#registration-email").click();
      await page.keyboard.press("Tab");
    },
  },
  {
    // A free address, then a weak password; the form is never sent. Asking
    // whether an address is free writes nothing (AvailabilityController).
    name: "guest-registration-weak",
    role: "guest",
    url: "/?action=register",
    whole: false,
    act: async (page) => {
      await page.locator("#registration-email").fill("newcomer@test.local");
      await button(page, "Продолжить").click();
      await page.locator("#registration-password").fill("abc");
    },
  },
  {
    name: "guest-registration-bad-email",
    role: "guest",
    url: "/?action=register",
    whole: false,
    act: async (page) => {
      await page.locator("#registration-email").fill("bad@");
      await page.keyboard.press("Tab");
    },
  },

  // Profile
  {
    name: "member-profile-rating",
    role: "user",
    url: "/users/RatingPositive",
  },
  {
    name: "guest-profile-offline",
    role: "guest",
    url: "/users/RatingDisabled",
  },
  {
    // Tab until the focus is in the table of games, and left there.
    name: "member-profile-assistants-focus",
    role: "user",
    url: "/users/SolohinLex?tab=games",
    act: async (page) => {
      for (let i = 0; i < 60; i++) {
        await page.keyboard.press("Tab");
        const inTable = await page.evaluate(
          () => !!document.activeElement?.closest("table"),
        );
        if (inTable) break;
      }
    },
  },
  {
    // The own name in other letters: the own profile is not recognised.
    name: "member-profile-own-lowercase",
    role: "user",
    url: "/users/testuser",
  },
  {
    name: "member-profile-games-player",
    role: "user",
    url: "/users/SolohinLex?tab=games",
    act: async (page) => {
      await button(page, "Игрок").click();
    },
  },
  {
    // An account that only plays: the table opens on the player's games.
    name: "member-profile-games-autoswitch",
    role: "user",
    url: (f) => `/users/${f.playerOnlyUsername}?tab=games`,
  },
  {
    name: "member-profile-other-topics",
    role: "user",
    url: "/users/SolohinLex",
    act: async (page) => {
      await page.getByRole("tab", { name: "Топики", exact: true }).click();
    },
  },
  {
    name: "member-profile-achievement-popover",
    role: "user",
    url: "/users/SolohinLex/achievements",
    whole: false,
    act: async (page) => {
      await page.locator("main button, main [tabindex='0']").first().hover();
    },
  },
  {
    name: "member-profile-achievements-blocks",
    role: "user",
    url: "/users/SolohinLex/achievements",
  },
  {
    // The mentor follows the administrator: "Подписан" opens the events of the
    // subscription, and nothing in it is changed.
    name: "mentor-profile-subscribe-menu",
    role: "mentor",
    url: "/users/SolohinLex",
    whole: false,
    act: async (page) => {
      await button(page, "Подписан").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "member-profile-subscribe-failed",
    role: "user",
    url: "/users/SolohinLex",
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/users\/me\/subscriptions$/,
        "POST",
        500,
        "Ошибка",
      );
      await button(page, "Подписаться").click();
    },
  },
  {
    name: "member-write-endorsement",
    role: "user",
    url: "/users/TestSeniorMod/write-endorsement",
    act: async (page) => {
      await page.getByRole("textbox").first().fill("коротко");
    },
  },
  {
    name: "guest-profile-received-endorsements",
    role: "guest",
    url: "/users/TestSeniorMod/received-endorsements",
  },
  {
    // The administrator wrote the endorsement: the clock an hour after it,
    // inside the day he may edit it, and then past that day.
    name: "admin-endorsements-edit-fresh",
    role: "admin",
    url: "/users/TestSeniorMod/received-endorsements",
    clock: (f) => minutesAfter(f.endorsementCreatedUtc, 60),
  },
  {
    name: "admin-endorsements-edit-late",
    role: "admin",
    url: "/users/TestSeniorMod/received-endorsements",
    clock: (f) => minutesAfter(f.endorsementCreatedUtc, 25 * 60),
  },
  {
    // A character added to the status, never saved.
    name: "admin-profile-own-edit",
    role: "admin",
    url: "/users/SolohinLex",
    act: async (page) => {
      await button(page, "Редактировать").click();
      const status = page.getByRole("textbox", { name: "Статус" });
      await status.press("End");
      await status.pressSequentially(".");
      await page.getByText("Несохраненные изменения").waitFor();
    },
  },
  {
    name: "admin-profile-own-uploads",
    role: "admin",
    url: "/users/SolohinLex/uploads",
  },
  {
    name: "member-community",
    role: "user",
    url: "/community",
  },
  {
    name: "guest-community-filter",
    role: "guest",
    url: "/community",
    whole: false,
    act: openFilter,
  },
  {
    // A query typed, then the search refused: what the table keeps.
    name: "guest-community-search-failed",
    role: "guest",
    url: "/community",
    act: async (page) => {
      const search = page.getByPlaceholder(/Поиск/).first();
      await search.fill("Pla");
      await page.waitForLoadState("networkidle");
      await refuse(page, /\/v1\/users\?/, "GET", 500, "Ошибка");
      await search.pressSequentially("y");
      await page.waitForLoadState("networkidle");
    },
  },

  // Moderation
  {
    name: "admin-award-types-refused",
    role: "admin",
    url: "/moderation/award-types",
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/moderation\/award-types\/[^/]+$/,
        "PATCH",
        400,
        "Тестовый отказ",
      );
      await button(page, /^(Скрыть|Вернуть)$/).click();
    },
  },
  {
    name: "admin-award-type-dialog",
    role: "admin",
    url: "/moderation/award-types",
    whole: false,
    act: async (page) => {
      await button(page, "Редактировать").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    // The series of the 23rd literary contest, whose id the catalogue fixes (AwardCatalog).
    name: "admin-award-series-refused",
    role: "admin",
    url: "/moderation/awards/series/00000000-0000-0000-0004-000000000001",
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/moderation\/contest-series\/[^/]+$/,
        "PATCH",
        400,
        "Тестовый отказ",
      );
      await page.getByLabel(/Активна/).click();
      await button(page, "Сохранить").click();
    },
  },
  {
    // A year the form takes and the server does not (AwardValidators.cs); nothing is sent.
    name: "admin-award-series-create-year",
    role: "admin",
    url: "/moderation/awards",
    whole: false,
    act: async (page) => {
      await button(page, "+ Новая серия").click();
      const dialog = page.getByRole("dialog");
      await dialog.waitFor();
      await dialog.getByLabel("Год").fill("2001");
    },
  },
  {
    name: "admin-achievements",
    role: "admin",
    url: "/moderation/achievements",
  },
  {
    name: "admin-achievement-category-dialog",
    role: "admin",
    url: "/moderation/achievements",
    whole: false,
    act: async (page) => {
      await button(page, "Редактировать").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-achievement-tier-dialog",
    role: "admin",
    url: "/moderation/achievements",
    whole: false,
    act: async (page) => {
      await button(page, /^Редактировать тир/).click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-tag-dialog",
    role: "admin",
    url: "/moderation/tags",
    whole: false,
    act: async (page) => {
      await button(page, "Редактировать тег").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-warn-dialog",
    role: "admin",
    url: "/users/RatingNegative",
    whole: false,
    act: async (page) => {
      await page.getByRole("tab", { name: "Модерация", exact: true }).click();
      await button(page, "Выдать предупреждение за профиль").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-ban-dialog",
    role: "admin",
    url: "/users/RatingNegative",
    whole: false,
    act: async (page) => {
      await page.getByRole("tab", { name: "Модерация", exact: true }).click();
      await button(page, "Выдать бан").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    // Experienced is banned already, and the server refuses a second ban
    // before it writes (BanService.CreateBan). Past the end of his ban the
    // request would ban him, so it goes out only while the ban runs.
    name: "admin-ban-repeat-refused",
    role: "admin",
    url: "/users/Experienced",
    whole: false,
    act: async (page) => {
      await page.route(/\/v1\/bans$/, async (route) => {
        if (route.request().method() !== "POST") return route.fallback();
        const api = new URL(route.request().url()).origin;
        const running = await page.request.get(
          `${api}/v1/users/Experienced/bans/active`,
        );
        return running.ok() ? route.fallback() : route.abort();
      });
      await page.getByRole("tab", { name: "Модерация", exact: true }).click();
      await button(page, "Выдать бан").click();
      const dialog = page.getByRole("dialog");
      await dialog.getByRole("textbox").first().fill("тест");
      await dialog.getByRole("button", { name: /Выдать|Забанить/ }).click();
    },
  },
  {
    // The one running ban of the list that can be lifted is Experienced's.
    name: "admin-unban-dialog",
    role: "admin",
    url: "/moderation/bans",
    whole: false,
    act: async (page) => {
      await page.getByText("Снять бан", { exact: true }).first().click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-points-dialog",
    role: "admin",
    url: "/moderation/warnings",
    whole: false,
    act: async (page) => {
      await page.getByText("Изменить баллы", { exact: true }).first().click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-fundraising",
    role: "admin",
    url: "/moderation/fundraising",
  },
  {
    name: "admin-removed-accounts-search-empty",
    role: "admin",
    url: "/moderation/removed-accounts",
    act: async (page) => {
      await page.getByPlaceholder("Имя или почта").fill("яяяя");
      await button(page, "Найти").click();
      await page.getByText("Ничего не найдено").waitFor();
    },
  },
  {
    name: "moderator-rated-posts",
    role: "moderator",
    url: "/moderation/rated-posts",
  },

  // Tickets
  {
    name: "guest-ticket-track-old-link",
    role: "guest",
    url: `/support/track/${GUEST_TICKET_TOKEN}`,
  },
  {
    // The answer of the server is made up, so no ticket is created.
    name: "guest-support-sent",
    role: "guest",
    url: "/support",
    act: async (page) => {
      await page.route(/\/v1\/tickets$/, (route) =>
        route.request().method() === "POST"
          ? route.fulfill({
              status: 201,
              contentType: "application/json",
              body: JSON.stringify({
                ticketId: "00000000-0000-0000-0000-0000000000d1",
                trackingToken: DEAD,
              }),
            })
          : route.fallback(),
      );
      await page.locator("#subtype").selectOption({ label: "Ошибка" });
      await page.locator("#contact").fill("guest@test.local");
      await page.getByRole("textbox").last().click();
      await page.keyboard.type("Тест");
      await button(page, "Отправить").click();
    },
  },
  {
    // The queue narrowed to one type of ticket.
    name: "moderator-tickets-subtype",
    role: "moderator",
    url: "/moderation/tickets",
    act: async (page) => {
      await page
        .locator("#ticket-subtype")
        .selectOption({ label: "Жалоба на пользователя" });
    },
  },

  // Editor
  {
    name: "moderator-editor-toolbar",
    role: "moderator",
    url: "/forum/general/4",
  },
  {
    // The check of the markup under the field, after the field loses the focus.
    name: "member-editor-private-warning",
    role: "user",
    url: "/forum/general/4",
    act: async (page) => {
      await button(page, "Режим BBCode").click();
      await page
        .getByRole("textbox", { name: "Написать комментарий..." })
        .fill("[private]тайна[/private]");
      await page.keyboard.press("Tab");
    },
  },
  {
    name: "member-editor-link-bad-address",
    role: "user",
    url: "/forum/general/4",
    whole: false,
    act: async (page) => {
      await page
        .getByRole("button", { name: "Вставить ссылку" })
        .last()
        .click();
      await page
        .getByRole("dialog")
        .getByLabel("URL ссылки")
        .fill("https://exa mple.com");
      await page.keyboard.press("Tab");
    },
  },
  {
    name: "member-editor-image-size-refused",
    role: "user",
    url: "/forum/general/4",
    whole: false,
    act: async (page) => {
      await page
        .getByRole("button", { name: "Вставить изображение" })
        .last()
        .click();
      await page.locator('input[id$="-width"]').last().fill("10000");
      await page.keyboard.press("Tab");
    },
  },
  {
    // The draft of the editor lives in the browser: typing writes nothing to the server.
    name: "guest-support-editor-typing",
    role: "guest",
    url: "/support",
    act: async (page) => {
      await page.locator("#subtype").selectOption({ label: "Ошибка" });
      await page.getByRole("textbox").last().click();
      await page.keyboard.type("тест");
    },
  },
  {
    name: "guest-support-editor-draft",
    role: "guest",
    url: "/support",
    act: async (page) => {
      await page.locator("#subtype").selectOption({ label: "Ошибка" });
      await page.getByRole("textbox").last().click();
      await page.keyboard.type("тест");
      await page.waitForTimeout(2000);
      await page.reload({ waitUntil: "load" });
      await page.locator("#subtype").selectOption({ label: "Ошибка" });
      await page.getByText("Есть черновик").waitFor();
    },
  },

  // Other pages
  {
    name: "guest-polls-filter",
    role: "guest",
    url: "/polls",
    whole: false,
    act: openFilter,
  },
  {
    name: "senior-poll-create-refused",
    role: "senior",
    url: "/polls",
    act: async (page) => {
      await refuse(page, /\/v1\/polls$/, "POST", 400, "Другая причина");
      await button(page, "+ Создать опрос").click();
      await page.locator("#poll-title").fill("Тест");
      await page.getByPlaceholder("Вариант 1").fill("Да");
      await page.getByPlaceholder("Вариант 2").fill("Нет");
      await button(page, "Создать опрос").click();
      // The open form outgrew the window stretched before it opened, and the
      // refusal scrolls the page in some runs and not in others: back to the top.
      await page.mouse.wheel(0, -5000);
    },
  },
  {
    name: "senior-poll-edit",
    role: "senior",
    url: "/polls",
    act: async (page) => {
      await button(page, "Редактировать опрос").click();
    },
  },
  {
    // The senior moderator has voted in a running poll; the member has not.
    name: "senior-poll-cancel-vote",
    role: "senior",
    url: "/polls",
    act: async (page) => {
      await refuse(page, /\/v1\/polls\/[^/]+\/vote/, "DELETE", 500, "Ошибка");
      await page
        .locator("main")
        .getByRole("button", { name: "Отменить голос" })
        .first()
        .click();
    },
  },
  {
    name: "member-testimonials",
    role: "user",
    url: "/testimonials",
    act: async (page) => {
      await page.getByRole("textbox").first().fill("тест");
    },
  },
  {
    // The search over the testimonials applies on Enter.
    name: "member-testimonials-wildcard",
    role: "user",
    url: "/testimonials",
    act: async (page) => {
      const search = page.getByPlaceholder("Поиск по тексту или автору");
      await search.fill("_");
      await search.press("Enter");
    },
  },
  {
    name: "guest-rules-expanded",
    role: "guest",
    url: "/rules",
    act: async (page) => {
      await button(page, "Развернуть все").click();
    },
  },
  {
    // The staff of the rules is asked by role, and every role is refused.
    name: "guest-rules-staff-failed",
    role: "guest",
    url: "/rules",
    act: async (page) => {
      await refuse(page, /\/v1\/users\?.*role=/, "GET", 500, "Ошибка");
      await page.reload({ waitUntil: "load" });
    },
  },
  {
    name: "guest-statistics-bad-month",
    role: "guest",
    url: "/statistics?year=2026&month=13",
  },
  {
    name: "guest-privacy",
    role: "guest",
    url: "/privacy",
  },
  {
    name: "guest-agreement",
    role: "guest",
    url: "/agreement",
  },
  {
    name: "guest-donate",
    role: "guest",
    url: "/donate",
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
