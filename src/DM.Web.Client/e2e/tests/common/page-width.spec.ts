import {
  test as base,
  expect,
  API_BASE_URL,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
} from "../../fixtures/auth";
import { LAYOUT_WIDTH, phoneContext } from "../../fixtures/phone";
import type { APIRequestContext, BrowserContext, Page } from "@playwright/test";

/**
 * No page is wider than the window on a phone (a phone is the full version:
 * it lays the page out at the width of a narrow window and the reader zooms
 * in, fixtures/phone.ts), and none between 1001 and 1279, where the shell
 * still stands in three columns and the middle one is narrower than a phone:
 * 300 at 1024, between two sidebars of 330 (the owner kept three columns
 * there; UI_STANDARDS, "Страница не шире окна"). The middle column takes the
 * width the shell leaves it and no more, so a page wider than that column no
 * longer pushes the right column off the edge of the window, as the home page
 * did at 1213 and the pulse at 1260. It runs under the right column instead,
 * which is worse to read and invisible to a check of the document alone. So
 * three things are read, as a reader sees them:
 *
 *  - neither the document nor the shell's scroller scrolls sideways;
 *  - the right column ends inside the window, where the page has one: the
 *    room of posts and the messenger take its place (router.ts);
 *  - no part of the middle column stands past its edges, the gutter of the
 *    column excluded. A part that a box of its own scrolls or clips is that
 *    box's to hold, and the box is what is read: a table scrolls in its frame
 *    only where even its least width, every word whole, is wider than the
 *    column (fixtures/tables.ts), and its frame keeps to the column. A layer
 *    fixed to the window (a menu, a calendar) is not a part of the column.
 *
 * 1280 is the first width past that range, and the one the boards of the
 * statistics and of the month's digest were three cramped columns at. On a
 * phone the right column is a sliding one and stands in no line of the page,
 * so the same things are read without it.
 *
 * Each page is opened on a phone and in a window, and the window is read at
 * every width in turn, each once it stops changing.
 */
const WIDTHS = [1024, 1280];

/** The widest window that is not the desktop shell. */
const SHELL_BREAKPOINT = 1000;

type Role = "guest" | "user" | "admin";

/** The session each role opens with; a guest has none. */
const SESSION: Record<Role, string | undefined> = {
  guest: undefined,
  user: SECONDARY_STORAGE_STATE,
  admin: PRIMARY_STORAGE_STATE,
};

/** A seeded game whose first room is open to a guest and has posts. */
const GAME_TITLE = "Хроники Забытых Королевств";

interface Target {
  name: string;
  roles: Role[];
  /** The address, or how to find it in the seed. */
  path: string | ((api: APIRequestContext) => Promise<string>);
  /** What has to be on the page before it is measured. */
  ready: string;
  /** A step after the address opened, to reach the state measured. */
  reach?: (page: Page) => Promise<void>;
  /** The route mounts no right column: the page stands in its place. */
  noRightColumn?: true;
}

/** The first ordinary room of the seeded game, by its address. */
async function roomPath(api: APIRequestContext): Promise<string> {
  const games = await api.get(`${API_BASE_URL}/v1/games?take=50`);
  expect(games.ok(), `GET /v1/games answered ${games.status()}`).toBe(true);
  const { resources } = await games.json();
  const game = (resources ?? []).find(
    (g: { title: string }) => g.title === GAME_TITLE,
  );
  expect(game, `the seed has no game "${GAME_TITLE}"`).toBeTruthy();
  const rooms = await api.get(`${API_BASE_URL}/v1/games/${game.id}/rooms`);
  expect(rooms.ok(), `GET rooms answered ${rooms.status()}`).toBe(true);
  const room = ((await rooms.json()).resources ?? []).find(
    (r: { type: string }) => r.type === "Default",
  );
  expect(room, `"${GAME_TITLE}" has no ordinary room`).toBeTruthy();
  return `/game/${game.publicId ?? game.id}/rooms/${room.roomNumber}`;
}

/**
 * The newest digest of a period ("Итоги ..."), the news topic its boards stand
 * in, by its address. The home page is no place to find it: it shows the news
 * of the past week, two at most (RecentNews, NEWS_WIDGET_LIMIT), and whether a
 * digest is among them depends on the day the stand was seeded. A digest is
 * dated the first of the next month, and the seed dates its other news days
 * before the seed, so a week after the first the home page has no digest.
 */
async function digestPath(api: APIRequestContext): Promise<string> {
  const topics = await api.get(
    `${API_BASE_URL}/v1/boards/news/topics?take=50&sortBy=created&sortOrder=desc`,
  );
  expect(topics.ok(), `GET news topics answered ${topics.status()}`).toBe(true);
  const digest = ((await topics.json()).resources ?? []).find(
    (t: { periodDigest?: unknown }) => t.periodDigest,
  );
  expect(
    digest,
    "the seed has no digest of a period among the news",
  ).toBeTruthy();
  return `/forum/news/${digest.topicNumber}`;
}

/** The token of the seeded guest ticket, the one with a known address. */
const SEEDED_GUEST_TICKET = "5eedc0de5eedc0de5eedc0de5eedc0de";

/**
 * The id of the seeded complaint about a moderator's decision, the ticket
 * whose conversation holds a question and an answer. Its author has no saved
 * session here, and the administrator reads it at both addresses: the server
 * answers a ticket to whoever moderates it.
 */
async function seededTicket(api: APIRequestContext): Promise<string> {
  const tickets = await api.get(
    `${API_BASE_URL}/v1/moderation/tickets?subtype=ModeratorDecisionComplaint`,
  );
  expect(
    tickets.ok(),
    `GET /v1/moderation/tickets answered ${tickets.status()}`,
  ).toBe(true);
  const ticket = ((await tickets.json()).resources ?? [])[0];
  expect(ticket, "the seed has no complaint about a decision").toBeTruthy();
  return ticket.id;
}

const PAGES: Target[] = [
  // The news of the home page. The administrator's news carry more actions.
  // Whichever news the week holds: the boards of a digest are read on its
  // own topic below, where they are always drawn.
  {
    name: "home",
    path: "/",
    roles: ["guest", "user", "admin"],
    ready: "main#main .news-list .topic",
  },
  // The boards of the month's digest, the ones that were three cramped columns
  // at 1280, on the news topic they belong to.
  {
    name: "digest topic",
    path: digestPath,
    roles: ["guest", "user", "admin"],
    ready: "main#main .digest-boards .stat-board",
  },
  {
    name: "pulse",
    path: "/pulse",
    roles: ["guest", "user"],
    ready: "main#main .filter-bar",
  },
  // The table of games is wider than the column: it scrolls in its frame
  // and the page stays put.
  {
    name: "games",
    path: "/games",
    roles: ["guest", "user"],
    ready: "main#main tr.table-row",
  },
  // A reader's own row on a board is bold, so a little wider.
  {
    name: "statistics",
    path: "/statistics",
    roles: ["guest", "user"],
    ready: "main#main .stat-board",
  },
  {
    name: "community",
    path: "/community",
    roles: ["guest"],
    ready: "main#main tr.table-row",
  },
  {
    name: "forum board",
    path: "/forum/general",
    roles: ["guest", "admin"],
    ready: "main#main .topics-table",
  },
  // A guest reads the room; a player has the form of a post under it, with
  // the row of tags; the master has his own controls.
  {
    name: "game room",
    path: roomPath,
    roles: ["guest", "user", "admin"],
    ready: "main#main .game-room",
    noRightColumn: true,
  },
  // A topic and its comments: the captions, the names, the form of a comment.
  {
    name: "topic",
    path: "/forum/general/5",
    roles: ["guest", "user", "admin"],
    ready: "main#main .comment",
  },
  // A profile: the avatar, the actions, the tabs.
  {
    name: "another's profile",
    path: "/users/TestModerator",
    roles: ["user"],
    ready: "main#main [role='tablist']",
  },
  {
    name: "a profile to a moderator",
    path: "/users/TestUser",
    roles: ["admin"],
    ready: "main#main [role='tablist']",
  },
  // The log of warnings: the captions of an entry and of a decision.
  {
    name: "log",
    path: "/warnings",
    roles: ["user"],
    ready: "main#main .warning-card",
  },
  // The staff, and where to turn.
  {
    name: "rules",
    path: "/rules",
    roles: ["guest"],
    ready: "main#main .help-row",
  },
  // The bans, each the sentence of DM2's bot, under the line of the search.
  {
    name: "bans",
    path: "/moderation/bans",
    roles: ["admin"],
    ready: "main#main .ban-log-card",
  },
  // The catalogue of award kinds: "Редактировать" and "Скрыть" of a card
  // stood past the card while the column of codes kept its width, past the
  // middle column at 1024; the codes now break where the column is narrower
  // than one of them.
  {
    name: "award types",
    path: "/moderation/award-types",
    roles: ["admin"],
    ready: "main#main .type-row",
  },
  // The feed of notifications and the line of what is unread over it, which
  // stands only while something is.
  {
    name: "notifications",
    path: "/notifications",
    roles: ["user"],
    ready: "main#main .unread-place",
  },
  // The account: the blacklist table with its field, the blocks of the
  // channels, the sessions with their addresses whole.
  {
    name: "account",
    path: "/account",
    roles: ["user"],
    ready: "main#main .session-item",
  },
  // A ticket: its block, the conversation, and the form of an answer, to a
  // moderator with the status over its field.
  {
    name: "a ticket to its author",
    path: async (api) => `/my-tickets/${await seededTicket(api)}`,
    roles: ["admin"],
    ready: "main#main .ticket-thread",
  },
  {
    name: "a ticket to a moderator",
    path: async (api) => `/moderation/tickets/${await seededTicket(api)}`,
    roles: ["admin"],
    ready: "main#main .ticket-answer-form",
  },
  {
    name: "a guest's ticket",
    path: `/support/track/${SEEDED_GUEST_TICKET}`,
    roles: ["guest"],
    ready: "main#main .ticket-thread",
  },
  {
    name: "global chat",
    path: "/global-chat",
    roles: ["guest", "user"],
    ready: "main#main .chat-feed",
  },
  {
    name: "messenger",
    path: "/messenger",
    roles: ["user"],
    ready: "main#main .chats-list",
    noRightColumn: true,
  },
  // The first conversation of the list, opened from it: an address that
  // starts one would write, and this check only reads.
  {
    name: "conversation",
    path: "/messenger",
    roles: ["user"],
    ready: "main#main .chat-view",
    noRightColumn: true,
    reach: async (page) => {
      await page.locator("main#main .chat-preview").first().click();
      await expect(page).toHaveURL(/\/messenger\/c\//);
    },
  },
];

/**
 * Everything wrong with the width of the page, as sentences. Read in the
 * page, so it is one function with no outside names.
 */
function pageWidth({
  desktop,
  rightColumn,
}: {
  desktop: boolean;
  rightColumn: boolean;
}): string[] {
  const problems: string[] = [];
  const scroller = document.querySelector<HTMLElement>(".main")!;
  const content = document.querySelector<HTMLElement>("main#main")!;
  const aside = document.querySelector<HTMLElement>("aside.sidebar-right");

  for (const [name, el] of [
    ["the document", document.scrollingElement!],
    ["the shell", scroller],
  ] as const) {
    if (el.scrollWidth > el.clientWidth + 1) {
      problems.push(
        `${name} scrolls sideways: ${el.scrollWidth} > ${el.clientWidth}`,
      );
    }
  }

  // The window as the page sees it: inside the shell's own scrollbar.
  const windowRight =
    scroller.getBoundingClientRect().left + scroller.clientWidth;
  if (!desktop) {
    // A sliding column stands in no line of the page.
  } else if (!rightColumn) {
    if (aside) problems.push("a right column stands where the page has none");
  } else if (!aside) {
    problems.push("the right column is not there");
  } else if (aside.getBoundingClientRect().right > windowRight + 0.5) {
    problems.push(
      `the right column ends at ${Math.round(aside.getBoundingClientRect().right)}, the window at ${Math.round(windowRight)}`,
    );
  }

  const box = content.getBoundingClientRect();
  const style = getComputedStyle(content);
  const left = box.left + parseFloat(style.paddingLeft);
  const right = box.right - parseFloat(style.paddingRight);

  /** A part a box of its own holds: one that clips or scrolls, or a layer. */
  const heldInside = (el: Element) => {
    for (let up = el.parentElement; up && up !== content; ) {
      const s = getComputedStyle(up);
      if (s.overflowX !== "visible" || s.position === "fixed") return true;
      up = up.parentElement;
    }
    return false;
  };
  const past = (el: Element) => {
    if (!el.getClientRects().length) return false;
    const s = getComputedStyle(el);
    if (s.visibility === "hidden" || s.position === "fixed") return false;
    const r = el.getBoundingClientRect();
    if (r.width < 1) return false;
    return (r.right > right + 1 || r.left < left - 1) && !heldInside(el);
  };
  const name = (el: Element) =>
    `${el.tagName.toLowerCase()}${[...el.classList].map((c) => `.${c}`).join("")}`;

  // Only the outermost part that stands out is named: everything inside it
  // stands out with it, and the list would bury the one that matters.
  for (const el of content.querySelectorAll("*")) {
    if (!past(el)) continue;
    if (el.parentElement !== content && past(el.parentElement!)) continue;
    const r = el.getBoundingClientRect();
    problems.push(
      `${name(el)} spans ${Math.round(r.left)}-${Math.round(r.right)}, the column ${Math.round(left)}-${Math.round(right)}`,
    );
  }
  return problems;
}

const test = base.extend<{
  /**
   * Opens a page as a role on a phone of 390 or at a width; the contexts close
   * after the test.
   */
  openAs: (
    role: Role,
    width: number | "phone",
    target: Target,
  ) => Promise<Page>;
}>({
  openAs: async ({ browser, playwright }, use) => {
    const contexts: BrowserContext[] = [];
    const requests: APIRequestContext[] = [];
    await use(async (role, width, target) => {
      const context =
        width === "phone"
          ? await phoneContext(browser, { storageState: SESSION[role] })
          : await browser.newContext({
              storageState: SESSION[role],
              viewport: { width, height: 900 },
            });
      contexts.push(context);
      let path = target.path;
      if (typeof path !== "string") {
        const api = await playwright.request.newContext({
          storageState: SESSION[role],
        });
        requests.push(api);
        path = await path(api);
      }
      const page = await context.newPage();
      await page.goto(path);
      await target.reach?.(page);
      return page;
    });
    for (const api of requests) await api.dispose();
    for (const context of contexts) await context.close();
  },
});

/**
 * The problems at a width once the page stops changing, or the last ones. A
 * phone is read as it is laid out, with no width set.
 */
async function readAt(
  page: Page,
  width: number | "phone",
  target: Target,
): Promise<string[]> {
  if (width !== "phone") await page.setViewportSize({ width, height: 900 });
  const laidOut = width === "phone" ? LAYOUT_WIDTH : width;
  let problems: string[] = [];
  for (let attempt = 0; attempt < 12; attempt++) {
    // A row settles in the frame after its column does.
    await page.evaluate(() =>
      document.fonts.ready.then(
        () =>
          new Promise<void>((done) =>
            requestAnimationFrame(() => requestAnimationFrame(() => done())),
          ),
      ),
    );
    await expect(page.locator("main#main .skeleton-row")).toHaveCount(0);
    await expect(
      page.locator("main#main .stat-board-skeleton-row"),
    ).toHaveCount(0);
    problems = await page.evaluate(pageWidth, {
      desktop: laidOut > SHELL_BREAKPOINT,
      rightColumn: !target.noRightColumn,
    });
    if (!problems.length) return problems;
    await page.waitForTimeout(250);
  }
  return problems.map((one) =>
    width === "phone" ? `a phone: ${one}` : `${width}px: ${one}`,
  );
}

test.describe("Pages no wider than the window", () => {
  // A page read on a phone and at two widths, each once it stops changing.
  test.describe.configure({ timeout: 120_000 });

  for (const target of PAGES) {
    for (const role of target.roles) {
      test(`${target.name} as ${role} keeps to the window on a phone and at ${WIDTHS.join(", ")}px`, async ({
        openAs,
      }) => {
        const problems: string[] = [];
        for (const screen of ["phone", WIDTHS[0]] as const) {
          const page = await openAs(role, screen, target);
          await expect(page.locator(target.ready).first()).toBeVisible({
            timeout: 15000,
          });
          // What answers after the page (comments, boards, the columns' own
          // lists) is measured in the face it is drawn with.
          await page.waitForLoadState("networkidle");
          if (screen === "phone")
            problems.push(...(await readAt(page, screen, target)));
          else
            for (const width of WIDTHS)
              problems.push(...(await readAt(page, width, target)));
        }
        expect(problems).toEqual([]);
      });
    }
  }
});
