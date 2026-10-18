import {
  test as base,
  expect,
  API_BASE_URL,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
} from "../../fixtures/auth";
import { phoneContext } from "../../fixtures/phone";
import { readTables } from "../../fixtures/tables";
import type { APIRequestContext, BrowserContext, Page } from "@playwright/test";

/**
 * Nothing is hidden and nothing is cut for want of room, at any width, a
 * phone included (UI_STANDARDS, "Ряд целиком"): no "Еще", no "…", no name
 * cut, no row scrolling sideways. Where a line has no room it wraps. A phone
 * lays the page out at the width of a narrow window and the reader zooms in
 * (fixtures/phone.ts), so what holds there holds on every phone.
 *
 * What is read is what a reader would miss:
 *  - a menu that holds what the page put away, and a button, a tab or a link
 *    that says nothing but "Еще", "ЕЩЕ" or "…";
 *  - a part of a caption, of a row of buttons or of a strip of tabs taken out
 *    with [hidden];
 *  - text under text-overflow: ellipsis, and text that stands past the edge of
 *    a box that clips it. A text for the reader of the screen alone
 *    (clip-path: inset(50%)) and a sign under aria-hidden are not text cut;
 *  - a caption broken inside a part that fits in the line, a bar " | " left
 *    standing at the end or the start of a line, a strip of tabs with a line
 *    that opens with "|";
 *  - a row of buttons that runs past its own edge (a row that has no room
 *    wraps onto more lines);
 *  - a box that scrolls sideways. A table and a list with columns are the one
 *    exception, and only where even their least width, every word whole, is
 *    wider than the column (fixtures/tables.ts); a column of a table left out
 *    is a part taken out like any other.
 *
 * Whether the page is wider than the window is page-width.spec.ts's, and the
 * words and the frames of the tables at every width are tables-fit.spec.ts's.
 *
 * Each page is opened on a phone and in a window, as the role whose rows are
 * the longest there, and the window is read at every width in turn, from the
 * narrowest. The administrator of the seed stands in for a moderator: the
 * actions he sees are a moderator's and more.
 */
const WIDTHS = [1024, 1366];

/** The seeded game of SolohinLex, the primary account, with its sheet. */
const MASTERED_GAME = "Хроники Забытых Королевств";

type Role = "guest" | "user" | "admin";

/** The session each role opens with; a guest has none. */
const SESSION: Record<Role, string | undefined> = {
  guest: undefined,
  user: SECONDARY_STORAGE_STATE,
  admin: PRIMARY_STORAGE_STATE,
};

/** A seeded game TestUser plays in, with a room he writes posts in. */
const PLAYED_GAME = "Наследие Драконов";

interface Target {
  /** The name of the test, where the address is found in the seed. */
  name?: string;
  path: string | ((api: APIRequestContext) => Promise<string>);
  roles: Role[];
  /** What has to be on the page before it is read. */
  ready: string;
}

/** The token of the seeded guest ticket, the one with a known address. */
const SEEDED_GUEST_TICKET = "5eedc0de5eedc0de5eedc0de5eedc0de";

/**
 * The seeded complaint about a moderator's decision, the ticket whose
 * conversation holds a question and an answer, by the address a moderator
 * opens it at.
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
  return `/moderation/tickets/${ticket.id}`;
}

/** The settings of the game the administrator of the seed is the master of. */
async function masteredGameSettings(api: APIRequestContext): Promise<string> {
  const games = await api.get(`${API_BASE_URL}/v1/games?take=100`);
  expect(games.ok(), `GET /v1/games answered ${games.status()}`).toBe(true);
  const game = ((await games.json()).resources ?? []).find(
    (g: { title: string }) => g.title === MASTERED_GAME,
  );
  expect(game, `the seed has no game "${MASTERED_GAME}"`).toBeTruthy();
  return `/game/${game.publicId ?? game.id}/settings`;
}

/** The first template of a sheet in the seed, by the address it is edited at. */
async function seededSchemaTemplate(api: APIRequestContext): Promise<string> {
  const templates = await api.get(
    `${API_BASE_URL}/v1/attribute-schema-templates`,
  );
  expect(
    templates.ok(),
    `GET /v1/attribute-schema-templates answered ${templates.status()}`,
  ).toBe(true);
  const template = ((await templates.json()).resources ?? [])[0];
  expect(template, "the seed has no template of a sheet").toBeTruthy();
  return `/moderation/schema-templates/${template.id}`;
}

/** The first room of the game TestUser plays in, by its address. */
async function playedRoom(api: APIRequestContext): Promise<string> {
  const games = await api.get(`${API_BASE_URL}/v1/games?take=100`);
  expect(games.ok(), `GET /v1/games answered ${games.status()}`).toBe(true);
  const game = ((await games.json()).resources ?? []).find(
    (g: { title: string }) => g.title === PLAYED_GAME,
  );
  expect(game, `the seed has no game "${PLAYED_GAME}"`).toBeTruthy();
  return `/game/${game.publicId ?? game.id}/rooms/1`;
}

/** The pages read, and the roles that see their longest rows. */
const PAGES: Target[] = [
  // The caption of the news and the column of the best post of the week.
  { path: "/", roles: ["guest"], ready: "main#main .news-list .topic" },
  // A topic and its comments: the captions with their actions and likes.
  {
    path: "/forum/general/5",
    roles: ["guest", "user", "admin"],
    ready: "main#main .comment",
  },
  // The tabs and the actions of a profile, one's own and another's.
  {
    path: "/users/TestUser",
    roles: ["user", "admin"],
    ready: "main#main [role='tablist']",
  },
  {
    path: "/users/TestModerator",
    roles: ["user"],
    ready: "main#main [role='tablist']",
  },
  // The rows of buttons of the moderation tab of a profile.
  {
    path: "/users/TestUser?tab=moderation",
    roles: ["admin"],
    ready: "main#main .button-row",
  },
  // The logs: the caption of every entry and of every decision.
  { path: "/warnings", roles: ["user"], ready: "main#main .warning-card" },
  {
    path: "/moderation/warnings",
    roles: ["admin"],
    ready: "main#main .caption-line",
  },
  // The entries of the bans, each the sentence of DM2's bot.
  {
    path: "/moderation/bans",
    roles: ["admin"],
    ready: "main#main .ban-log-card",
  },
  // The tables and the rows of actions in the pages of the site's settings.
  {
    path: "/moderation/awards",
    roles: ["admin"],
    ready: "main#main .awards-admin tr.table-row",
  },
  {
    path: "/moderation/achievements",
    roles: ["admin"],
    ready: "main#main .caption-line",
  },
  {
    path: "/moderation/schema-templates",
    roles: ["admin"],
    ready: "main#main .template-row",
  },
  // The table of the attributes of a sheet: in the form of a new game, in the
  // settings of a game and in a template.
  { path: "/games/create", roles: ["user"], ready: "main#main .attr-table" },
  {
    name: "the settings of a game",
    path: masteredGameSettings,
    roles: ["admin"],
    ready: "main#main .attr-table",
  },
  {
    name: "a template of a sheet",
    path: seededSchemaTemplate,
    roles: ["admin"],
    ready: "main#main .attr-table",
  },
  // The filter bars with the words of their buttons, and the tables.
  { path: "/games", roles: ["guest"], ready: "main#main tr.table-row" },
  { path: "/community", roles: ["guest"], ready: "main#main tr.table-row" },
  // The names of the staff and the rows of where to turn.
  { path: "/rules", roles: ["guest"], ready: "main#main .help-row" },
  // The feed of the chat, its search and its jump to a date.
  { path: "/global-chat", roles: ["user"], ready: "main#main .chat-message" },
  { path: "/messenger", roles: ["user"], ready: "main#main .chat-preview" },
  // The feed of notifications: the titles, the dates and the details under
  // them. The line of what is unread stands only while something is.
  {
    path: "/notifications",
    roles: ["user"],
    ready: "main#main .unread-place",
  },
  // The account: the blacklist with its line of adding under the table, the
  // channels and the captions of the sessions.
  { path: "/account", roles: ["user"], ready: "main#main .session-item" },
  // A ticket: the caption of its block, the conversation and the form of an
  // answer with the status of moderation.
  {
    name: "a ticket to a moderator",
    path: seededTicket,
    roles: ["admin"],
    ready: "main#main .ticket-answer-form",
  },
  {
    path: `/support/track/${SEEDED_GUEST_TICKET}`,
    roles: ["guest"],
    ready: "main#main .ticket-thread",
  },
  // The form of a post with the tag bar of the editor.
  { path: playedRoom, roles: ["user"], ready: "main#main .editor-toolbar" },
];

/**
 * Everything hidden or cut on the page, as sentences. Read in the page, so it
 * is one function with no outside names.
 */
function hiddenOrCut(): string[] {
  const problems: string[] = [];
  const name = (el: Element) =>
    `${el.tagName.toLowerCase()}${[...el.classList].map((c) => `.${c}`).join("")}`;
  const words = (el: Element) =>
    ((el as HTMLElement).innerText ?? el.textContent ?? "")
      .replace(/\s+/g, " ")
      .trim()
      .slice(0, 60);
  const shown = (el: Element) =>
    el.getClientRects().length > 0 &&
    el.checkVisibility({ visibilityProperty: true });

  // A menu of what the row put away, and a control that names only it.
  for (const menu of document.querySelectorAll(".more-menu")) {
    problems.push(`a menu of the rest: ${name(menu)}`);
  }
  const PUT_AWAY = new Set(["Еще", "ЕЩЕ", "…"]);
  for (const control of document.querySelectorAll(
    "button, a, [role='tab'], [role='button']",
  )) {
    if (!shown(control)) continue;
    const said = [
      (control as HTMLElement).innerText?.trim() ?? "",
      control.getAttribute("aria-label")?.trim() ?? "",
    ];
    if (said.some((one) => PUT_AWAY.has(one)))
      problems.push(`a control of the rest: ${name(control)} "${said}"`);
  }

  // A part of a caption, of a row of buttons or of a strip of tabs taken out.
  for (const hidden of document.querySelectorAll(
    ".caption-line [hidden], .button-row [hidden], [role='tablist'] [hidden]",
  )) {
    problems.push(`a part taken out: ${name(hidden)} "${words(hidden)}"`);
  }

  // Text under an ellipsis. A field is left out: its value is not text of
  // the page, and the browser's own sheet puts an ellipsis on a file field.
  for (const el of document.querySelectorAll("body *")) {
    if (el.matches("input, select, textarea")) continue;
    if (getComputedStyle(el).textOverflow === "ellipsis")
      problems.push(`an ellipsis on ${name(el)} "${words(el)}"`);
  }

  // Text past the edge of a box that clips it. Up the tree from each text to
  // the first box that scrolls or the first layer fixed to the window: a box
  // above either clips nothing of it.
  const SKIP = "script, style, noscript, template, svg, select, textarea";
  const cut = new Set<Element>();
  const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    const holder = node.parentElement;
    if (!holder || !(node as Text).data.trim()) continue;
    if (holder.closest(SKIP) || holder.closest("[aria-hidden='true']"))
      continue;
    if (!shown(holder)) continue;
    const range = document.createRange();
    range.selectNodeContents(node);
    const boxes = [...range.getClientRects()].filter((r) => r.width > 0.5);
    if (!boxes.length) continue;
    const clipping: Element[] = [];
    let forReader = false;
    for (let up: Element | null = holder; up; up = up.parentElement) {
      const style = getComputedStyle(up);
      if (style.clipPath === "inset(50%)") {
        forReader = true;
        break;
      }
      if (style.overflowX === "hidden" || style.overflowX === "clip") {
        const frame = up.getBoundingClientRect();
        const left = frame.left + parseFloat(style.borderLeftWidth);
        const right = frame.right - parseFloat(style.borderRightWidth);
        if (boxes.some((r) => r.left < left - 1 || r.right > right + 1))
          clipping.push(up);
      }
      // Past the edge of a box that scrolls, the text is scrolled to, not
      // cut; whether that box may scroll is read below.
      if (style.overflowX === "auto" || style.overflowX === "scroll") break;
      if (style.position === "fixed") break;
    }
    if (!forReader) for (const box of clipping) cut.add(box);
  }
  for (const box of cut)
    problems.push(`text cut by the edge of ${name(box)} "${words(box)}"`);

  // A caption breaks between its parts only, and a bar never ends a line.
  const blockOf = (el: Element) => {
    let up = el.parentElement;
    while (up && getComputedStyle(up).display.startsWith("inline"))
      up = up.parentElement;
    return up;
  };
  const textBoxes = (el: Element) => {
    const out: DOMRect[] = [];
    const inside = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    for (let node = inside.nextNode(); node; node = inside.nextNode()) {
      if (!(node as Text).data.trim()) continue;
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const r of range.getClientRects()) if (r.width > 0.5) out.push(r);
    }
    return out;
  };
  const linesOf = (el: Element) => {
    const tops = textBoxes(el)
      .map((r) => r.top)
      .sort((a, b) => a - b);
    const half = parseFloat(getComputedStyle(el).fontSize) * 0.6;
    let lines = 0;
    let last = -Infinity;
    for (const top of tops)
      if (top - last > half) {
        lines += 1;
        last = top;
      }
    return lines;
  };
  for (const part of document.querySelectorAll(".caption-line__part")) {
    if (!shown(part)) continue;
    if (getComputedStyle(part).display === "inline") continue;
    const block = blockOf(part);
    if (!block) continue;
    const style = getComputedStyle(block);
    const inner =
      block.getBoundingClientRect().width -
      parseFloat(style.paddingLeft) -
      parseFloat(style.paddingRight) -
      parseFloat(style.borderLeftWidth) -
      parseFloat(style.borderRightWidth);
    if (linesOf(part) > 1 && part.getBoundingClientRect().width < inner - 1)
      problems.push(
        `a caption breaks inside a part that fits: "${words(part)}"`,
      );
  }
  for (const bar of document.querySelectorAll(".caption-line__bar")) {
    const gap = bar.parentElement!;
    if (!shown(gap)) continue;
    const drawn = bar.getBoundingClientRect();
    const room = gap.getBoundingClientRect();
    const visible =
      Math.min(drawn.right, room.right) - Math.max(drawn.left, room.left);
    if (visible <= 1) continue;
    const before = gap.previousElementSibling;
    const after = gap.nextElementSibling;
    // A part with no text of its own (a like without a count) is its box.
    const end =
      before && (textBoxes(before).at(-1) ?? before.getBoundingClientRect());
    const start =
      after && (textBoxes(after)[0] ?? after.getBoundingClientRect());
    const oneLine =
      !!end &&
      !!start &&
      Math.min(end.bottom, start.bottom) - Math.max(end.top, start.top) > 2;
    if (!oneLine)
      problems.push(
        `a bar at the break of a caption: "${before ? words(before) : ""}" | "${after ? words(after) : ""}"`,
      );
  }

  // A strip of tabs breaks after a bar, so no line of it opens with one.
  for (const strip of document.querySelectorAll("[role='tablist']")) {
    if (!shown(strip)) continue;
    const firsts = new Map<number, { left: number; text: string }>();
    const inside = document.createTreeWalker(strip, NodeFilter.SHOW_TEXT);
    for (let node = inside.nextNode(); node; node = inside.nextNode()) {
      const text = (node as Text).data.trim();
      if (!text) continue;
      const range = document.createRange();
      range.selectNodeContents(node);
      const box = [...range.getClientRects()].find((r) => r.width > 0.5);
      if (!box) continue;
      const line = Math.round(box.top);
      const known = [...firsts.keys()].find((top) => Math.abs(top - line) < 4);
      const key = known ?? line;
      const first = firsts.get(key);
      if (!first || box.left < first.left)
        firsts.set(key, { left: box.left, text });
    }
    for (const { text } of firsts.values())
      if (text.startsWith("|"))
        problems.push(`a line of tabs opens with a bar: "${words(strip)}"`);
  }

  // A row of buttons keeps to its own edge: one that has no room wraps
  // onto more lines.
  for (const row of document.querySelectorAll(".button-row")) {
    if (!shown(row)) continue;
    const edge = row.getBoundingClientRect().right;
    const past = [...row.children]
      .filter(shown)
      .some((item) => item.getBoundingClientRect().right > edge + 1);
    if (past)
      problems.push(`a row of buttons runs past its edge: "${words(row)}"`);
  }

  // Nothing scrolls sideways but a table and a list with columns, and they
  // only where their least width, every word whole, is wider than their box
  // (fixtures/tables.ts). The frame of a DataTable is read there; the frame
  // of a table of its own (the attributes of a sheet) is read here the same
  // way; a list holds its rows to the least width it measured
  // (ExpandableList).
  for (const el of document.querySelectorAll("body, body *")) {
    if (!shown(el)) continue;
    const { overflowX } = getComputedStyle(el);
    if (overflowX !== "auto" && overflowX !== "scroll") continue;
    if (el.scrollWidth <= el.clientWidth + 1) continue;
    if (el.matches(".data-table-scroll")) continue;
    const table = el.children.length === 1 ? el.firstElementChild : null;
    if (table instanceof HTMLTableElement) {
      // Read at no width of its own and put back at once, before anything
      // is drawn, as fixtures/tables.ts reads a DataTable.
      const style = table.getAttribute("style");
      table.style.setProperty("width", "0px", "important");
      table.style.setProperty("min-width", "0px", "important");
      const least = table.getBoundingClientRect().width;
      if (style === null) table.removeAttribute("style");
      else table.setAttribute("style", style);
      if (least > el.clientWidth + 1) continue;
    }
    if (el.matches(".expandable-list--columns")) {
      const least = Math.max(
        0,
        ...[...el.children].map(
          (row) => parseFloat(getComputedStyle(row).minWidth) || 0,
        ),
      );
      if (least > el.clientWidth + 1) continue;
    }
    problems.push(
      `${name(el)} scrolls sideways: ${el.scrollWidth} > ${el.clientWidth}`,
    );
  }
  return problems;
}

/** Two frames after the face has loaded: a row settles after its column. */
async function settle(page: Page) {
  await page.evaluate(() =>
    document.fonts.ready.then(
      () =>
        new Promise<void>((done) =>
          requestAnimationFrame(() => requestAnimationFrame(() => done())),
        ),
    ),
  );
}

/**
 * The problems at a width once the page stops changing, or the last ones. A
 * phone is read as it is laid out, with no width set. The tables are read for
 * their columns and their frames; their words are tables-fit.spec.ts's.
 */
async function readAt(page: Page, width: number | "phone"): Promise<string[]> {
  if (width !== "phone") await page.setViewportSize({ width, height: 900 });
  let problems: string[] = [];
  for (let attempt = 0; attempt < 12; attempt++) {
    await settle(page);
    await expect(page.locator("main#main .skeleton-row")).toHaveCount(0);
    const tables = await page.evaluate(readTables, {
      within: "main#main",
      words: false,
    });
    problems = [...(await page.evaluate(hiddenOrCut)), ...tables.problems];
    if (!problems.length) return problems;
    await page.waitForTimeout(250);
  }
  return problems.map((one) =>
    width === "phone" ? `a phone: ${one}` : `${width}px: ${one}`,
  );
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
      return page;
    });
    for (const api of requests) await api.dispose();
    for (const context of contexts) await context.close();
  },
});

test.describe("Nothing hidden and nothing cut", () => {
  // A page read on a phone and at two widths, each once it stops changing.
  test.describe.configure({ timeout: 120_000 });

  for (const target of PAGES) {
    const where =
      target.name ?? (typeof target.path === "string" ? target.path : "a room");
    for (const role of target.roles) {
      test(`${where} as ${role} hides and cuts nothing on a phone and at ${WIDTHS.join(", ")}px`, async ({
        openAs,
      }) => {
        const problems: string[] = [];
        for (const screen of ["phone", WIDTHS[0]] as const) {
          const page = await openAs(role, screen, target);
          await expect(page.locator(target.ready).first()).toBeVisible({
            timeout: 15000,
          });
          // What answers after the page (comments, the log, the columns) is
          // read in the face it is drawn with.
          await page.waitForLoadState("networkidle");
          if (screen === "phone")
            problems.push(...(await readAt(page, screen)));
          else
            for (const width of WIDTHS)
              problems.push(...(await readAt(page, width)));
        }
        expect(problems).toEqual([]);
      });
    }
  }
});

/**
 * The tag bar of the editor in the middle column of a desktop: every tag in
 * sight, no "…", and the switch [bbcode]/wysiwyg inside the editor, on a line
 * of its own under the tags where the widest group and the switch do not fit
 * side by side. At 1001 and 1024 the column is the narrowest it gets.
 */
const EDITOR_WIDTHS = [1001, 1024, 1366];

test.describe("The tag bar of the editor on a desktop", () => {
  for (const target of [
    { path: "/forum/general/5", ready: "main#main .editor-toolbar" },
    { path: playedRoom, ready: "main#main .editor-toolbar" },
  ] satisfies Omit<Target, "roles">[]) {
    const where = typeof target.path === "string" ? target.path : "a room";
    test(`${where} shows every tag and keeps the switch inside at ${EDITOR_WIDTHS.join(", ")}px`, async ({
      openAs,
    }) => {
      const page = await openAs("user", EDITOR_WIDTHS[0], {
        ...target,
        roles: ["user"],
      });
      await expect(page.locator(target.ready).first()).toBeVisible({
        timeout: 15000,
      });
      const problems: string[] = [];
      for (const width of EDITOR_WIDTHS) {
        await page.setViewportSize({ width, height: 900 });
        await settle(page);
        const bar = await page.evaluate(() => {
          const wrapper = document.querySelector(
            "main#main .bbcode-editor-wrapper",
          )!;
          const frame = wrapper.getBoundingClientRect();
          const tags = [...wrapper.querySelectorAll(".tag-btn")];
          const mode = wrapper.querySelector(".mode-tabs")!;
          const switchBox = mode.getBoundingClientRect();
          return {
            tags: tags.length,
            hidden: tags.filter((tag) => !tag.getClientRects().length).length,
            more: wrapper.querySelectorAll(".tag-btn-more, .more-menu").length,
            switchInside:
              switchBox.left >= frame.left - 0.5 &&
              switchBox.right <= frame.right + 0.5,
          };
        });
        if (bar.tags < 16) problems.push(`${width}px: ${bar.tags} tags`);
        if (bar.hidden) problems.push(`${width}px: ${bar.hidden} tags hidden`);
        if (bar.more) problems.push(`${width}px: a menu of the rest`);
        if (!bar.switchInside)
          problems.push(`${width}px: the switch stands out of the editor`);
      }
      expect(problems).toEqual([]);
    });
  }
});

/**
 * The menus of the left column in the sliding column of a phone. A row of a
 * menu is a row too: one line, its count beside its name. The
 * labels of moderation are written in the client, so a longer one is caught
 * here before a moderator sees it break; the boards of the forum are read as
 * the seed names them. The administrator sees every row of moderation.
 */
const MENUS: { path: string; role: Role; list: string }[] = [
  {
    path: "/moderation/bans",
    role: "admin",
    list: "#sidebar-list-ModerationPanel",
  },
  { path: "/forum/general", role: "user", list: "#sidebar-list-ForumBoards" },
];

test.describe("Menus of the left column on one line", () => {
  for (const { path, role, list } of MENUS) {
    test(`${list} on ${path} as ${role} keeps every row on one line on a phone`, async ({
      openAs,
    }) => {
      const page = await openAs(role, "phone", {
        path,
        roles: [role],
        ready: "",
      });
      // The page first: the column goes back on every change of address
      // (App.vue), and a press before the first navigation ended opened a
      // column that the navigation closed again.
      await expect(page.locator("main#main h1").first()).toBeVisible({
        timeout: 15000,
      });
      await page.getByRole("button", { name: "Меню", exact: true }).click();
      const rows = page
        .getByRole("dialog", { name: "Меню" })
        .locator(`${list} > li`);
      await expect(rows.locator("a").first()).toBeVisible({ timeout: 15000 });
      await page.evaluate(() => document.fonts.ready);
      // A row taller than its line stands on two.
      const broken = await rows.evaluateAll((items) =>
        items
          .filter(
            (li) =>
              li.getBoundingClientRect().height >
              parseFloat(getComputedStyle(li).lineHeight) + 1,
          )
          .map((li) => (li.textContent ?? "").trim()),
      );
      expect(await rows.count()).toBeGreaterThan(9);
      expect(broken).toEqual([]);
    });
  }
});

/**
 * The menu of the header (UI_STANDARDS, "Шапка"). From 1001 up it stands
 * between the logo and the statistics, from the left, on the line the text of
 * the page starts on, as the menu of DM2 starts right after its logo; it
 * wraps there into a second line of the same size. In the smallest step, 15,
 * it gives half of its sides to the links and starts 16 left of that line, so
 * that eight links hold in two lines down to the breakpoint; nine take a
 * third a few widths over it (menuSteps.spec.ts). The header stays 100, and
 * the lines stand in the middle of its height, level with the statistics.
 * The widths around the steps of eight links and of nine; the administrator
 * has the ninth link.
 */
const DESKTOP_WIDTHS = [1920, 1366, 1260, 1259, 1134, 1100, 1068, 1024, 1001];

test.describe("The menu of the header", () => {
  for (const role of ["user", "admin"] as const) {
    for (const width of DESKTOP_WIDTHS) {
      test(`stands from the left between the logo and the statistics as ${role} at ${width}px`, async ({
        openAs,
      }) => {
        const page = await openAs(role, width, {
          path: "/",
          roles: [role],
          ready: "",
        });
        await expect(
          page.locator("header nav.top-menu .link").first(),
        ).toBeVisible();
        await expect(page.locator("header .site-stats")).toBeVisible({
          timeout: 15000,
        });
        await page.evaluate(() => document.fonts.ready);
        const fit = await page.evaluate(() => {
          const box = (selector: string) =>
            document.querySelector(selector)!.getBoundingClientRect();
          const links = [
            ...document.querySelectorAll("header nav.top-menu .link"),
          ].map((link) => link.getBoundingClientRect());
          const lines = new Map<number, DOMRect[]>();
          for (const link of links) {
            const top = Math.round(link.top);
            lines.set(top, [...(lines.get(top) ?? []), link]);
          }
          const main = document.querySelector<HTMLElement>("main#main")!;
          const stats = box("header .site-stats");
          const scroller = document.scrollingElement!;
          return {
            header: box("header.header").height,
            logoColumn: box("header .user-info").right,
            statsColumn: box("header .stats-col").left,
            // Where the text of the page starts under the menu.
            text:
              main.getBoundingClientRect().left +
              parseFloat(getComputedStyle(main).paddingLeft),
            starts: [...lines.values()].map((line) =>
              Math.min(...line.map((b) => b.left)),
            ),
            right: Math.max(...links.map((b) => b.right)),
            lines: lines.size,
            size: parseFloat(
              getComputedStyle(
                document.querySelector("header nav.top-menu .link")!,
              ).fontSize,
            ),
            menuMiddle:
              (Math.min(...links.map((b) => b.top)) +
                Math.max(...links.map((b) => b.bottom))) /
              2,
            statsMiddle: (stats.top + stats.bottom) / 2,
            overflow: scroller.scrollWidth - scroller.clientWidth,
          };
        });
        expect(fit.header).toBe(100);
        // Every line starts where the text of the page starts, half a
        // gutter before it in the smallest step.
        const lead = fit.size > 15 ? 0 : -16;
        expect(fit.starts.map((start) => Math.round(start - fit.text))).toEqual(
          fit.starts.map(() => lead),
        );
        expect(fit.starts[0]).toBeGreaterThanOrEqual(fit.logoColumn);
        expect(fit.right).toBeLessThanOrEqual(fit.statsColumn);
        // Two lines, a third only for nine links in the smallest step.
        expect(fit.lines).toBeLessThanOrEqual(
          role === "admin" && fit.size === 15 ? 3 : 2,
        );
        // The full size wherever two lines of it fit, 1366 among them.
        if (width >= 1366) expect(fit.size).toBe(25);
        expect(Math.abs(fit.menuMiddle - fit.statsMiddle)).toBeLessThanOrEqual(
          2,
        );
        expect(fit.overflow).toBeLessThanOrEqual(1);
      });
    }
  }
});
