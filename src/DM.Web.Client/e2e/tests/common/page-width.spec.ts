import { expect, API_BASE_URL } from "../../fixtures/auth";
import { test, type Target as RoleTarget } from "../../fixtures/roles";
import {
  SEEDED_GUEST_TICKET,
  longestLoginGame,
  masteredGameRoom,
  seededTicketId,
} from "../../fixtures/seed";
import type { APIRequestContext, Page } from "@playwright/test";
import { settle } from "../../fixtures/window";

/**
 * No page is wider than the window (UI_STANDARDS, "Страница не шире окна").
 * A page wider than the middle column
 * runs under the right one, which a check of the document alone does not see,
 * so three things are read, as a reader sees them:
 *
 *  - neither the document nor the shell's scroller scrolls sideways;
 *  - the right column ends inside the window, where the page has one: the
 *    room of posts and the messenger take its place (router.ts);
 *  - no part of the middle column stands past its edges, the gutter of the
 *    column excluded. A part that a box of its own scrolls or clips is that
 *    box's to hold (a table scrolls in its frame, fixtures/tables.ts). A layer
 *    fixed to the window (a menu, a calendar) is not a part of the column.
 *
 * Each page is opened in a window, and the window is read at every width in
 * turn, each once it stops changing.
 */
const WIDTHS = [1366, 1920];

/** A page to read, and how it stands: the roles of fixtures/roles.ts. */
interface Target extends RoleTarget {
  name: string;
  /** The route mounts no right column: the page stands in its place. */
  noRightColumn?: true;
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
  // The boards of the month's digest, on the news topic they belong to.
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
  // The details of a game whose master has the longest login of the seed: a
  // name of the details breaks inside itself rather than widen the column.
  {
    name: "game details",
    path: longestLoginGame,
    roles: ["guest"],
    ready: "main#main .info-table",
  },
  // A guest reads the room; a player has the form of a post under it, with
  // the row of tags; the master has his own controls.
  {
    name: "game room",
    path: masteredGameRoom,
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
  // stood past the card while the column of codes kept its width; the codes
  // now break where the column is narrower than one of them.
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
    path: async (api) => `/my-tickets/${await seededTicketId(api)}`,
    roles: ["admin"],
    ready: "main#main .ticket-thread",
  },
  {
    name: "a ticket to a moderator",
    path: async (api) => `/moderation/tickets/${await seededTicketId(api)}`,
    roles: ["admin"],
    ready: "main#main .ticket-answer-form",
  },
  {
    name: "a guest's ticket",
    path: `/support/track#token=${SEEDED_GUEST_TICKET}`,
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
function pageWidth({ rightColumn }: { rightColumn: boolean }): string[] {
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
  if (!rightColumn) {
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

/** The problems at a width once the page stops changing, or the last ones. */
async function readAt(
  page: Page,
  width: number,
  target: Target,
): Promise<string[]> {
  await page.setViewportSize({ width, height: 900 });
  let problems: string[] = [];
  for (let attempt = 0; attempt < 12; attempt++) {
    // A row settles in the frame after its column does.
    await settle(page);
    await expect(page.locator("main#main .skeleton-row")).toHaveCount(0);
    await expect(
      page.locator("main#main .stat-board-skeleton-row"),
    ).toHaveCount(0);
    problems = await page.evaluate(pageWidth, {
      rightColumn: !target.noRightColumn,
    });
    if (!problems.length) return problems;
    await page.waitForTimeout(250);
  }
  return problems.map((one) => `${width}px: ${one}`);
}

test.describe("Pages no wider than the window", () => {
  // A page read at two widths, each once it stops changing.
  test.describe.configure({ timeout: 120_000 });

  for (const target of PAGES) {
    for (const role of target.roles) {
      test(`${target.name} as ${role} keeps to the window at ${WIDTHS.join(", ")}px`, async ({
        openAs,
      }) => {
        const problems: string[] = [];
        const page = await openAs(role, WIDTHS[0], target);
        await expect(page.locator(target.ready).first()).toBeVisible({
          timeout: 15000,
        });
        // What answers after the page (comments, boards, the columns' own
        // lists) is measured in the face it is drawn with.
        await page.waitForLoadState("networkidle");
        for (const width of WIDTHS)
          problems.push(...(await readAt(page, width, target)));
        expect(problems).toEqual([]);
      });
    }
  }
});
