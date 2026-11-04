import { expect } from "../../fixtures/auth";
import { test, type Target } from "../../fixtures/roles";
import {
  masteredGameSettings,
  playedRoom,
  seededSchemaTemplate,
  seededTicket,
  SEEDED_GUEST_TICKET,
} from "../../fixtures/seed";
import { readInPage } from "../../fixtures/inPage";
import { readTables } from "../../fixtures/tables";
import { settle } from "../../fixtures/window";
import type { Page } from "@playwright/test";

/**
 * Nothing is hidden and nothing is cut for want of room, at any width
 * (UI_STANDARDS, "Ряд целиком"): no "Еще", no "…", no name cut, no row
 * scrolling sideways. Where a line has no room it wraps.
 *
 * What is read is what a reader would miss, one reader for each way a row
 * loses a part:
 *  - putAway: a menu that holds what the page put away, and a button, a tab or
 *    a link that says nothing but "Еще", "ЕЩЕ" or "…"; a part of a caption, of
 *    a row of buttons or of a strip of tabs taken out with [hidden];
 *  - textCut: text under text-overflow: ellipsis, and text that stands past
 *    the edge of a box that clips it. A text for the reader of the screen
 *    alone (clip-path: inset(50%)) and a sign under aria-hidden are not text
 *    cut;
 *  - captionBreaks: a caption broken inside a part that fits in the line, a
 *    bar " | " left standing at the end or the start of a line;
 *  - tabStrips: a strip of tabs with a line that opens with "|";
 *  - buttonRows: a row of buttons that runs past its own edge (a row that has
 *    no room wraps onto more lines);
 *  - sidewaysScroll: a box that scrolls sideways. A table and a list with
 *    columns are the one exception, and only where even their least width,
 *    every word whole, is wider than the column (fixtures/tables.ts); a column
 *    of a table left out is a part taken out like any other.
 *
 * Each reader runs in the page, so it is one function with no outside names;
 * hiddenOrCut runs them together at one moment (fixtures/inPage.ts).
 *
 * Whether the page is wider than the window is page-width.spec.ts's, and the
 * words and the frames of the tables at every width are tables-fit.spec.ts's.
 *
 * Each page is opened in a window, as the role whose rows are the longest
 * there (fixtures/roles.ts), and the window is read at every width in turn,
 * from the narrowest.
 */
const WIDTHS = [1366, 1920];

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
    path: `/support/track#token=${SEEDED_GUEST_TICKET}`,
    roles: ["guest"],
    ready: "main#main .ticket-thread",
  },
  // The form of a post with the tag bar of the editor.
  { path: playedRoom, roles: ["user"], ready: "main#main .editor-toolbar" },
];

/**
 * A menu of what the row put away, a control that names only it, and a part
 * of a caption, of a row of buttons or of a strip of tabs taken out.
 */
function putAway(): string[] {
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

  for (const hidden of document.querySelectorAll(
    ".caption-line [hidden], .button-row [hidden], [role='tablist'] [hidden]",
  )) {
    problems.push(`a part taken out: ${name(hidden)} "${words(hidden)}"`);
  }
  return problems;
}

/**
 * Text under an ellipsis, and text past the edge of a box that clips it. A
 * field is left out of the first: its value is not text of the page, and the
 * browser's own sheet puts an ellipsis on a file field. For the second, the
 * tree is read up from each text to the first box that scrolls or the first
 * layer fixed to the window: a box above either clips nothing of it.
 */
function textCut(): string[] {
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

  for (const el of document.querySelectorAll("body *")) {
    if (el.matches("input, select, textarea")) continue;
    if (getComputedStyle(el).textOverflow === "ellipsis")
      problems.push(`an ellipsis on ${name(el)} "${words(el)}"`);
  }

  // The boxes that clip a text, none for a text for the reader of the
  // screen alone.
  const clippersOf = (holder: Element, boxes: DOMRect[]) => {
    const clipping: Element[] = [];
    for (let up: Element | null = holder; up; up = up.parentElement) {
      const style = getComputedStyle(up);
      if (style.clipPath === "inset(50%)") return [];
      if (style.overflowX === "hidden" || style.overflowX === "clip") {
        const frame = up.getBoundingClientRect();
        const left = frame.left + parseFloat(style.borderLeftWidth);
        const right = frame.right - parseFloat(style.borderRightWidth);
        if (boxes.some((r) => r.left < left - 1 || r.right > right + 1))
          clipping.push(up);
      }
      // Past the edge of a box that scrolls, the text is scrolled to, not
      // cut; whether that box may scroll is sidewaysScroll's.
      if (style.overflowX === "auto" || style.overflowX === "scroll") break;
      if (style.position === "fixed") break;
    }
    return clipping;
  };
  const SKIP = "script, style, noscript, template, svg, select, textarea";
  const cut = new Set<Element>();
  const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    const holder = node.parentElement;
    if (!holder || !(node as Text).data.trim()) continue;
    if (holder.closest(SKIP) || holder.closest("[aria-hidden='true']"))
      continue;
    if (!shown(holder)) continue;
    // The letters, a word at a time: under pre-wrap the space at a break
    // hangs past the end of its line, and a space past the edge cuts nothing.
    const text = node as Text;
    const boxes = [...text.data.matchAll(/\S+/g)].flatMap((word) => {
      const range = document.createRange();
      range.setStart(text, word.index);
      range.setEnd(text, word.index + word[0].length);
      return [...range.getClientRects()].filter((r) => r.width > 0.5);
    });
    if (!boxes.length) continue;
    for (const box of clippersOf(holder, boxes)) cut.add(box);
  }
  for (const box of cut)
    problems.push(`text cut by the edge of ${name(box)} "${words(box)}"`);
  return problems;
}

/**
 * A caption breaks between its parts only, and a bar never ends a line. A
 * part with no text of its own (a like without a count) is its box.
 */
function captionBreaks(): string[] {
  const problems: string[] = [];
  const words = (el: Element) =>
    ((el as HTMLElement).innerText ?? el.textContent ?? "")
      .replace(/\s+/g, " ")
      .trim()
      .slice(0, 60);
  const shown = (el: Element) =>
    el.getClientRects().length > 0 &&
    el.checkVisibility({ visibilityProperty: true });
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
    const half = parseFloat(getComputedStyle(el).fontSize) * 0.6;
    let lines = 0;
    let last = -Infinity;
    for (const { top } of textBoxes(el).sort((a, b) => a.top - b.top))
      if (top - last > half) {
        lines += 1;
        last = top;
      }
    return lines;
  };
  const oneLine = (before: Element | null, after: Element | null) => {
    const end =
      before && (textBoxes(before).at(-1) ?? before.getBoundingClientRect());
    const start =
      after && (textBoxes(after)[0] ?? after.getBoundingClientRect());
    return (
      !!end &&
      !!start &&
      Math.min(end.bottom, start.bottom) - Math.max(end.top, start.top) > 2
    );
  };
  for (const part of document.querySelectorAll(".caption-line__part")) {
    if (!shown(part)) continue;
    if (getComputedStyle(part).display === "inline") continue;
    let block = part.parentElement;
    while (block && getComputedStyle(block).display.startsWith("inline"))
      block = block.parentElement;
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
    if (!oneLine(before, after))
      problems.push(
        `a bar at the break of a caption: "${before ? words(before) : ""}" | "${after ? words(after) : ""}"`,
      );
  }
  return problems;
}

/** A strip of tabs breaks after a bar, so no line of it opens with one. */
function tabStrips(): string[] {
  const problems: string[] = [];
  const words = (el: Element) =>
    ((el as HTMLElement).innerText ?? el.textContent ?? "")
      .replace(/\s+/g, " ")
      .trim()
      .slice(0, 60);
  const shown = (el: Element) =>
    el.getClientRects().length > 0 &&
    el.checkVisibility({ visibilityProperty: true });

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
  return problems;
}

/**
 * A row of buttons keeps to its own edge: one that has no room wraps onto
 * more lines.
 */
function buttonRows(): string[] {
  const problems: string[] = [];
  const words = (el: Element) =>
    ((el as HTMLElement).innerText ?? el.textContent ?? "")
      .replace(/\s+/g, " ")
      .trim()
      .slice(0, 60);
  const shown = (el: Element) =>
    el.getClientRects().length > 0 &&
    el.checkVisibility({ visibilityProperty: true });

  for (const row of document.querySelectorAll(".button-row")) {
    if (!shown(row)) continue;
    const edge = row.getBoundingClientRect().right;
    const past = [...row.children]
      .filter(shown)
      .some((item) => item.getBoundingClientRect().right > edge + 1);
    if (past)
      problems.push(`a row of buttons runs past its edge: "${words(row)}"`);
  }
  return problems;
}

/**
 * Nothing scrolls sideways but a table and a list with columns, and they only
 * where their least width, every word whole, is wider than their box
 * (fixtures/tables.ts). The frame of a DataTable is read there, and the frame
 * of a table of its own (the attributes of a sheet, a list with columns) is
 * read here the same way.
 */
function sidewaysScroll(): string[] {
  const problems: string[] = [];
  const name = (el: Element) =>
    `${el.tagName.toLowerCase()}${[...el.classList].map((c) => `.${c}`).join("")}`;
  const shown = (el: Element) =>
    el.getClientRects().length > 0 &&
    el.checkVisibility({ visibilityProperty: true });

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
    problems.push(
      `${name(el)} scrolls sideways: ${el.scrollWidth} > ${el.clientWidth}`,
    );
  }
  return problems;
}

/**
 * Everything hidden or cut on the page, as sentences: reader after reader in
 * the order above, all of them at one moment.
 */
async function hiddenOrCut(page: Page): Promise<string[]> {
  const found = await readInPage(
    page,
    [putAway, textCut, captionBreaks, tabStrips, buttonRows, sidewaysScroll],
    null,
  );
  return found.flat();
}

/**
 * The problems at a width once the page stops changing, or the last ones. The
 * tables are read for their columns and their frames; their words are
 * tables-fit.spec.ts's.
 */
async function readAt(page: Page, width: number): Promise<string[]> {
  await page.setViewportSize({ width, height: 900 });
  let problems: string[] = [];
  for (let attempt = 0; attempt < 12; attempt++) {
    await settle(page);
    await expect(page.locator("main#main .skeleton-row")).toHaveCount(0);
    const tables = await readTables(page, {
      within: "main#main",
      words: false,
    });
    problems = [...(await hiddenOrCut(page)), ...tables.problems];
    if (!problems.length) return problems;
    await page.waitForTimeout(250);
  }
  return problems.map((one) => `${width}px: ${one}`);
}

test.describe("Nothing hidden and nothing cut", () => {
  // A page read at two widths, each once it stops changing.
  test.describe.configure({ timeout: 120_000 });

  for (const target of PAGES) {
    const where =
      target.name ?? (typeof target.path === "string" ? target.path : "a room");
    for (const role of target.roles) {
      test(`${where} as ${role} hides and cuts nothing at ${WIDTHS.join(", ")}px`, async ({
        openAs,
      }) => {
        const problems: string[] = [];
        const page = await openAs(role, WIDTHS[0], target);
        await expect(page.locator(target.ready).first()).toBeVisible({
          timeout: 15000,
        });
        // What answers after the page (comments, the log, the columns) is
        // read in the face it is drawn with.
        await page.waitForLoadState("networkidle");
        for (const width of WIDTHS)
          problems.push(...(await readAt(page, width)));
        expect(problems).toEqual([]);
      });
    }
  }
});
