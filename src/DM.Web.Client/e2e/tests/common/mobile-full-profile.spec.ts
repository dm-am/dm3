import {
  test,
  expect,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
} from "../../fixtures/auth";
import { PHONES, phoneContext } from "../../fixtures/phone";
import type { Browser, Page } from "@playwright/test";

/**
 * A profile on a phone is the profile of the full version (the owner, 13
 * October 2026: no layout of the phone's own, the reader zooms in). A phone
 * lays the page out as a narrow window (fixtures/phone.ts) and shows it whole.
 *
 * The avatar is 220 wide, as at 1366, and not 280. Every action of the row
 * stands, in one line, each at its own width, as at 1366; none goes into a
 * square "…" and none goes down under another. Every tab of the strip stands:
 * a line of the strip breaks after a bar and never opens with one, and no tab
 * goes into "ЕЩЕ". The bar of the edit mode says its state in words, the words
 * never broken inside and nothing past the edge of the bar.
 *
 * Read as TestUser and as the administrator of the seed (SolohinLex), whose
 * sessions the global setup saves. Nothing is written: a request that would
 * write is refused on its way out, and the edit mode is left unsaved.
 */

/** The phones the owner's frames are taken on, and the full version. */
const WIDTHS = [...PHONES, 1366];

type Reader = "user" | "admin";

const SESSION: Record<Reader, string> = {
  user: SECONDARY_STORAGE_STATE,
  admin: PRIMARY_STORAGE_STATE,
};

/**
 * The profiles read: whose, by whom, the actions that stand there in their
 * order (the subscription and the blacklist say either of their two words),
 * and the tabs.
 */
const PROFILES: {
  path: string;
  reader: Reader;
  actions: RegExp[];
  tabs: string[];
}[] = [
  {
    path: "/users/TestModerator",
    reader: "user",
    actions: [
      /^Написать сообщение$/,
      /^Подпис(аться|ан)$/,
      /^(В черный список|Убрать из черного списка)$/,
    ],
    tabs: ["О себе", "Игры", "Блоги", "Топики", "Зал славы"],
  },
  {
    path: "/users/TestUser",
    reader: "admin",
    actions: [
      /^Написать сообщение$/,
      /^Подпис(аться|ан)$/,
      /^Написать рекомендацию$/,
      /^(В черный список|Убрать из черного списка)$/,
    ],
    tabs: ["О себе", "Игры", "Блоги", "Топики", "Зал славы", "Модерация"],
  },
];

/**
 * Opens a path as a reader on a phone of a width, or at 1366, every write
 * refused on its way out.
 */
async function open(
  browser: Browser,
  reader: Reader,
  width: number,
  path: string,
): Promise<Page> {
  const context =
    width < 1000
      ? await phoneContext(browser, { width, storageState: SESSION[reader] })
      : await browser.newContext({
          storageState: SESSION[reader],
          viewport: { width, height: 900 },
        });
  const page = await context.newPage();
  await page.route(
    (url) => url.pathname.startsWith("/v1/"),
    (route) =>
      ["GET", "HEAD", "OPTIONS"].includes(route.request().method())
        ? route.fallback()
        : route.abort(),
  );
  await page.goto(path);
  await expect(page.locator(".profile-page .actions")).toBeVisible({
    timeout: 15000,
  });
  await expect(page.locator(".profile-page .info-section")).toBeVisible();
  await page.evaluate(() => document.fonts.ready);
  return page;
}

type Actions = {
  /** The words of the items of the row that are drawn, in their order. */
  drawn: string[];
  /** How many items of the row are in it, drawn or not. */
  count: number;
  /** All the items start on the line of the first. */
  line: boolean;
  /** How far an item stands past the right edge of the row. */
  overrun: number;
  /** A square "…" or a menu that holds what left the row. */
  putAway: number;
};

function readActions(page: Page): Promise<Actions> {
  return page.evaluate(() => {
    const row = document.querySelector<HTMLElement>(".profile-page .actions")!;
    const edge = row.getBoundingClientRect().right;
    const children = [...row.children] as HTMLElement[];
    const items = children.filter(
      (el) => !el.hidden && el.getClientRects().length > 0,
    );
    const boxes = items.map((el) => el.getBoundingClientRect());
    const first = boxes[0];
    const line = boxes.every((box) => box.top < first.bottom - 1);
    const putAway =
      document.querySelectorAll(".profile-page .more-menu").length +
      children.filter((el) => (el.textContent ?? "").trim() === "…").length;
    return {
      drawn: items.map((el) => (el.textContent ?? "").trim()),
      count: children.length,
      line,
      overrun: Math.max(0, ...boxes.map((box) => box.right - edge)),
      putAway,
    };
  });
}

type Strip = {
  /** The tabs that are drawn, in their order. */
  drawn: string[];
  /** The lines of the strip, its tabs and bars in their order. */
  lines: string[];
  /** A line opens with a bar. */
  barOpens: boolean;
  /** "ЕЩЕ", "Еще" or "…" anywhere in the strip. */
  more: boolean;
  /** How far a piece of the strip stands past its right edge. */
  overrun: number;
  /** How far the strip scrolls sideways. */
  scroll: number;
};

function readStrip(page: Page): Promise<Strip> {
  return page.evaluate(() => {
    const strip = document.querySelector<HTMLElement>(
      ".profile-page .info-section",
    )!;
    const edge = strip.getBoundingClientRect().right;
    type Piece = { text: string; bar: boolean; box: DOMRect };
    const pieces: Piece[] = [];
    for (const tab of strip.querySelectorAll<HTMLElement>('[role="tab"]')) {
      const box = tab.getBoundingClientRect();
      if (tab.closest("[hidden]") || box.width === 0) continue;
      pieces.push({ text: (tab.textContent ?? "").trim(), bar: false, box });
    }
    const walker = document.createTreeWalker(strip, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      const at = (node.textContent ?? "").indexOf("|");
      if (at < 0 || node.parentElement?.closest("[hidden]")) continue;
      const range = document.createRange();
      range.setStart(node, at);
      range.setEnd(node, at + 1);
      const box = range.getBoundingClientRect();
      if (box.width > 0) pieces.push({ text: "|", bar: true, box });
    }
    const lines: { middle: number; pieces: Piece[] }[] = [];
    for (const piece of pieces) {
      const middle = (piece.box.top + piece.box.bottom) / 2;
      let line = lines.find((one) => Math.abs(one.middle - middle) < 6);
      if (!line) lines.push((line = { middle, pieces: [] }));
      line.pieces.push(piece);
    }
    lines.sort((a, b) => a.middle - b.middle);
    for (const line of lines)
      line.pieces.sort((a, b) => a.box.left - b.box.left);
    return {
      drawn: pieces.filter((p) => !p.bar).map((p) => p.text),
      lines: lines.map((line) => line.pieces.map((p) => p.text).join(" ")),
      barOpens: lines.some((line) => line.pieces[0].bar),
      more: /ЕЩЕ|Еще|…/.test(strip.textContent ?? ""),
      overrun: Math.max(0, ...pieces.map((p) => p.box.right - edge)),
      scroll: strip.scrollWidth - strip.clientWidth,
    };
  });
}

test.describe("A profile on a phone is the profile of the full version", () => {
  for (const { path, reader, actions, tabs } of PROFILES) {
    for (const width of WIDTHS) {
      test(`${path} as ${reader} at ${width}px: every action, every tab, the avatar 220`, async ({
        browser,
      }) => {
        const page = await open(browser, reader, width, path);

        // The recommendation and the tab "Модерация" come with answers of
        // their own, after the profile; the row decides when it is drawn, again
        // when an item changes and when the font arrives.
        await expect
          .poll(async () => {
            const row = await readActions(page);
            const strip = await readStrip(page);
            return (
              row.count === actions.length &&
              strip.drawn.length === tabs.length &&
              row.line
            );
          })
          .toBe(true);
        const row = await readActions(page);
        expect(row.count, row.drawn.join(", ")).toBe(actions.length);
        expect(row.drawn).toHaveLength(actions.length);
        row.drawn.forEach((words, i) => expect(words).toMatch(actions[i]));
        expect(row.line).toBe(true);
        expect(row.putAway).toBe(0);
        expect(row.overrun).toBeLessThanOrEqual(0.5);

        const strip = await readStrip(page);
        expect(strip.drawn.slice().sort()).toEqual(tabs.slice().sort());
        expect(strip.drawn[0]).toBe("О себе");
        expect(strip.more, strip.lines.join(" / ")).toBe(false);
        expect(strip.barOpens, strip.lines.join(" / ")).toBe(false);
        expect(strip.overrun).toBeLessThanOrEqual(0.5);
        expect(strip.scroll).toBeLessThanOrEqual(0);

        // A phone shows the layout scaled to its screen, and the box comes
        // back through that scale with a hundred-thousandth of a pixel more or
        // less, so it is read to the tenth.
        const avatar = await page
          .locator(".profile-page .avatar-wrapper")
          .boundingBox();
        expect(avatar?.width).toBeCloseTo(220, 1);

        await page.context().close();
      });
    }
  }

  /** The words of the bar, the bar and what stands past its edge. */
  async function readBar(page: Page) {
    return page.evaluate(() => {
      const bar = document.querySelector<HTMLElement>(".save-bar")!;
      const status = bar.querySelector<HTMLElement>(".save-bar-status")!;
      const edge = bar.getBoundingClientRect().right;
      // A word broken inside stands on two lines.
      const broken: string[] = [];
      const node = [...status.childNodes].find(
        (n) => n.nodeType === Node.TEXT_NODE,
      );
      if (node) {
        let at = 0;
        for (const word of (node.textContent ?? "").split(/(\s+)/)) {
          if (word.trim()) {
            const range = document.createRange();
            range.setStart(node, at);
            range.setEnd(node, at + word.length);
            const lines = new Set(
              [...range.getClientRects()]
                .filter((r) => r.width > 0)
                .map((r) => Math.round(r.top)),
            );
            if (lines.size > 1) broken.push(word);
          }
          at += word.length;
        }
      }
      const buttons = [...bar.querySelectorAll<HTMLElement>("button")];
      const tops = new Set(
        buttons.map((b) => Math.round(b.getBoundingClientRect().top)),
      );
      const box = status.getBoundingClientRect();
      return {
        words: status.textContent?.trim() ?? "",
        shown:
          getComputedStyle(status).visibility === "visible" &&
          box.width > 0 &&
          box.height > 0,
        broken,
        buttons: buttons.map((b) => (b.textContent ?? "").trim()),
        oneLineOfButtons: tops.size === 1,
        overrun: Math.max(
          0,
          ...[status, ...buttons].map(
            (el) => el.getBoundingClientRect().right - edge,
          ),
        ),
        scroll: bar.scrollWidth - bar.clientWidth,
      };
    });
  }

  for (const width of WIDTHS) {
    test(`the bar of the edit mode says its words at ${width}px`, async ({
      browser,
    }) => {
      const page = await open(browser, "user", width, "/users/TestUser");
      await page
        .locator(".profile-page .actions")
        .getByRole("button", { name: "Редактировать" })
        .click();
      await expect(page.locator(".save-bar")).toBeVisible();

      const mode = await readBar(page);
      expect(mode.words).toBe("Режим редактирования");
      expect(mode.shown).toBe(true);
      expect(mode.broken).toEqual([]);
      expect(mode.buttons).toEqual(["Завершить"]);
      expect(mode.overrun).toBeLessThanOrEqual(0.5);
      expect(mode.scroll).toBeLessThanOrEqual(0);

      // A change typed and never saved.
      await page.locator("#profile-status").fill("Проверка полосы");
      await expect(page.locator(".save-bar-status")).toHaveText(
        "Несохраненные изменения",
      );
      const changed = await readBar(page);
      expect(changed.shown).toBe(true);
      expect(changed.broken).toEqual([]);
      expect(changed.buttons).toEqual(["Сохранить", "Отмена"]);
      expect(changed.oneLineOfButtons).toBe(true);
      expect(changed.overrun).toBeLessThanOrEqual(0.5);
      expect(changed.scroll).toBeLessThanOrEqual(0);

      await page.context().close();
    });
  }
});
