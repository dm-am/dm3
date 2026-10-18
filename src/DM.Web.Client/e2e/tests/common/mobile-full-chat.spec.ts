import { test, expect, SECONDARY_STORAGE_STATE } from "../../fixtures/auth";
import {
  LAYOUT_WIDTH,
  pastTheLayout,
  phoneContext,
} from "../../fixtures/phone";
import type { Browser, Page, Route } from "@playwright/test";

/**
 * The chats on a phone are the chats of the full version: a phone lays the
 * page out as a narrow window and shows it whole, smaller (the owner, 13
 * October 2026: no layout of the phone's own; the reader zooms in).
 *
 * The global chat draws every avatar 72 wide. "Перейти к дате" says its
 * words, and the search beside it keeps room for a query, the button under it
 * where there is none (the middle column of a window just over 1000). The
 * header of a message says the whole name and the time, wrapping where the
 * line has no room. The events line keeps "ближайший" with its day, and the
 * calendar shows the day that was picked. Nothing of the page stands past the
 * layout of a phone.
 *
 * The list of correspondences says the whole name of the other one and the
 * whole excerpt of the last message, with the date beside the name wherever
 * the two fit, and a correspondence says whom it is with whole, in the line
 * at the top of its frame.
 *
 * Every name is replaced by a login of twenty wide letters, the longest a
 * login can be (UsernamePolicy), so that no line fits a phone whatever the
 * seed writes. Nothing is written: the answers of the reads are changed on
 * their way to the page, and every write the page would make is answered
 * here and never reaches the server.
 */

/**
 * The phones the owner's frames are taken on, by the width of their screens
 * (each lays the page out ${LAYOUT_WIDTH} wide), and the full version.
 */
const WIDTHS = [360, 390, 430, 1366];

/**
 * The global chat also at 1001, where it stands in the middle column of the
 * three, narrower than on a phone (277).
 */
const CHAT_WIDTHS = [360, 390, 430, 1001, 1366];

/** A screen narrower than a tablet is a phone. */
const isPhone = (width: number) => width < 768;

/**
 * The least the search takes beside "Перейти к дате": room for a query, not
 * only for its own words.
 */
const FIELD_LEAST = 160;

/**
 * Where the field would be narrower than that beside the button, and the
 * button stands under it: the middle column at 1001 (277 for both). One line
 * elsewhere, a phone included.
 */
const BUTTON_UNDER_FIELD = [1001];

/** The phone and the desktop the calendar of the date is checked on. */
const CALENDAR_WIDTHS = [390, 1366];

/** A login of twenty wide letters. */
const LONG = "Ш".repeat(20);

/** A last message longer than the excerpt the list shows (60 letters). */
const LONG_TEXT =
  "Договорились, на выходных напишу в комнате, а пока перечитай заметки игры про механику боя";

/** The excerpt of it the list writes: its first 60 letters and "...". */
const EXCERPT = LONG_TEXT.slice(0, 60) + "...";

type Named = { username?: string } | null | undefined;

/**
 * Answers every write of the page here, so the stand keeps what it had: the
 * read marks with "done", anything else refused. The live hub is let through,
 * it writes nothing.
 */
async function writeNothing(page: Page) {
  await page.route(
    (url) => /\/v1\//.test(url.pathname),
    async (route: Route) => {
      const method = route.request().method();
      if (method === "GET" || method === "OPTIONS" || method === "HEAD")
        return route.fallback();
      if (method === "DELETE" && /\/unread$/.test(route.request().url()))
        return route.fulfill({ status: 204 });
      return route.abort();
    },
  );
}

/** Changes the answer of a read on its way to the page. */
async function rewrite<T>(
  page: Page,
  path: RegExp,
  change: (answer: T) => void,
) {
  await page.route(
    (url) => path.test(url.pathname),
    async (route) => {
      if (route.request().method() !== "GET") return route.fallback();
      const response = await route.fetch();
      const answer = (await response.json()) as T;
      change(answer);
      await route.fulfill({ response, json: answer });
    },
  );
}

/**
 * Closes the page once its routes are let go. The page keeps reading after
 * the test has read it (the unread count, the newest messages), and a read
 * caught by `rewrite` while the context closes fails its fetch, and the test
 * fails on a request nobody waits for, the more often the slower the stand
 * answers.
 */
async function close(page: Page) {
  await page.unrouteAll({ behavior: "ignoreErrors" });
  await page.context().close();
}

async function openPage(
  browser: Browser,
  width: number,
  signedIn: boolean,
): Promise<Page> {
  const storageState = signedIn ? SECONDARY_STORAGE_STATE : undefined;
  const context = isPhone(width)
    ? await phoneContext(browser, { width, storageState })
    : await browser.newContext({
        viewport: { width, height: 844 },
        ...(storageState ? { storageState } : {}),
      });
  const page = await context.newPage();
  await writeNothing(page);
  return page;
}

/** The letters of an element that stand past a right edge, by how far. */
function overrunOf(page: Page, selector: string, edgeOf: string) {
  return page.evaluate(
    ([selector, edgeOf]) =>
      [...document.querySelectorAll<HTMLElement>(selector)].map((el) => {
        const edge = (el.closest(edgeOf) ?? el).getBoundingClientRect().right;
        let far = -Infinity;
        const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
        for (let node = walker.nextNode(); node; node = walker.nextNode()) {
          if (!node.textContent?.trim()) continue;
          const range = document.createRange();
          range.selectNodeContents(node);
          for (const rect of range.getClientRects())
            if (rect.width > 0) far = Math.max(far, rect.right);
        }
        return far - edge;
      }),
    [selector, edgeOf],
  );
}

/** What of an element is put away or cut: its text, if anything is. */
function putAwayIn(page: Page, selector: string) {
  return page.evaluate(
    (selector) =>
      [...document.querySelectorAll<HTMLElement>(selector)]
        .filter((el) =>
          [el, ...el.querySelectorAll<HTMLElement>("*")].some(
            (part) =>
              part.hidden ||
              getComputedStyle(part).textOverflow === "ellipsis" ||
              (part.matches("a, button") &&
                /^(Еще|ЕЩЕ|…)$/.test(part.innerText.trim())),
          ),
        )
        .map((el) => el.innerText),
    selector,
  );
}

test.describe("The global chat on a phone", () => {
  for (const width of CHAT_WIDTHS) {
    test(`draws the avatars 72 wide and says every word at ${width}`, async ({
      browser,
    }) => {
      const page = await openPage(browser, width, true);
      await rewrite<{ resources?: { author?: Named }[] }>(
        page,
        /\/v1\/global-chat\/messages$/,
        (answer) => {
          for (const message of answer.resources ?? [])
            if (message.author) message.author.username = LONG;
        },
      );
      await page.goto("/global-chat");
      const avatars = page.locator(".chat-feed .msg-avatar");
      await expect(avatars.first()).toBeVisible({ timeout: 15000 });
      await page.evaluate(() => document.fonts.ready);

      // The pictures of the full version, every one of them.
      const sizes = await avatars.evaluateAll((all) =>
        all.map((img) => Math.round(img.getBoundingClientRect().width)),
      );
      expect(sizes.length).toBeGreaterThan(0);
      expect(sizes.filter((size) => size !== 72)).toEqual([]);
      const gutters = await page
        .locator(".chat-feed .msg-time-gutter")
        .evaluateAll((all) =>
          all.map((gutter) => Math.round(gutter.getBoundingClientRect().width)),
        );
      expect(gutters.filter((size) => size !== 72)).toEqual([]);

      // "Перейти к дате" in words, and the search beside it whole.
      const date = page.locator(".chat-search-row .date-btn");
      await expect(date).toHaveText("Перейти к дате");
      const row = await page.evaluate(() => {
        const row = document.querySelector(".chat-search-row")!;
        const button = row.querySelector(".date-btn")!;
        const input = row.querySelector<HTMLInputElement>("input")!;
        const style = getComputedStyle(input);
        const canvas = document.createElement("canvas").getContext("2d")!;
        canvas.font = `${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
        const field = row
          .querySelector(".search-container")!
          .getBoundingClientRect();
        return {
          right: row.getBoundingClientRect().right,
          button: button.getBoundingClientRect().right,
          buttonLeft: button.getBoundingClientRect().left,
          buttonTop: button.getBoundingClientRect().top,
          fieldLeft: field.left,
          fieldTop: field.top,
          fieldBottom: field.bottom,
          fieldWidth: field.width,
          buttonCut: button.scrollWidth - button.clientWidth,
          room: input.clientWidth,
          words: canvas.measureText(input.placeholder).width,
        };
      });
      expect(row.button).toBeLessThanOrEqual(row.right + 0.5);
      expect(row.buttonCut).toBeLessThanOrEqual(0);
      expect(row.room).toBeGreaterThanOrEqual(Math.floor(row.words));
      expect(row.fieldWidth).toBeGreaterThanOrEqual(FIELD_LEAST - 0.5);
      // One line where the field keeps its least beside the button, the
      // button under the field, at its start, where it would not.
      if (BUTTON_UNDER_FIELD.includes(width)) {
        expect(row.buttonTop).toBeGreaterThanOrEqual(row.fieldBottom);
        expect(Math.abs(row.buttonLeft - row.fieldLeft)).toBeLessThan(1);
      } else expect(Math.abs(row.buttonTop - row.fieldTop)).toBeLessThan(1);

      // The calendar of the date hangs under the row and keeps within it, on
      // one line and on two: hung from the button at the start of the second
      // line, it reached over the column beside the chat.
      await date.click();
      const calendar = page.locator(".chat-search-row .date-popover");
      await expect(calendar).toBeVisible();
      const hung = await page.evaluate(() => {
        const row = document.querySelector(".chat-search-row")!;
        const box = (selector: string) =>
          row.querySelector(selector)!.getBoundingClientRect();
        return {
          row: box(".filter-bar"),
          button: box(".date-btn"),
          calendar: box(".date-popover"),
        };
      });
      expect(hung.calendar.left).toBeGreaterThanOrEqual(hung.row.left - 0.5);
      expect(hung.calendar.right).toBeLessThanOrEqual(hung.row.right + 0.5);
      expect(hung.calendar.top).toBeGreaterThanOrEqual(hung.button.bottom);
      await page.keyboard.press("Escape");
      await expect(calendar).toBeHidden();

      // The header of a message: the whole name and the time, nothing put
      // away, no letter past the edge of the message.
      const headers = page.locator(".chat-feed .msg-header");
      const texts = await headers.evaluateAll((all) =>
        all.map((header) => (header as HTMLElement).innerText),
      );
      expect(texts.length).toBeGreaterThan(0);
      expect(
        texts.filter(
          (text) =>
            !text.replace(/\s+/g, "").includes(LONG) ||
            !/\d{1,2}:\d{2}/.test(text),
        ),
      ).toEqual([]);
      expect(await putAwayIn(page, ".chat-feed .msg-header")).toEqual([]);
      const titled = await page
        .locator(".chat-feed .msg-author[title]")
        .count();
      expect(titled).toBe(0);
      expect(
        (
          await overrunOf(page, ".chat-feed .msg-header", ".chat-feed__message")
        ).filter((overrun) => overrun > 0.5),
      ).toEqual([]);

      if (isPhone(width))
        expect(await page.evaluate(pastTheLayout, LAYOUT_WIDTH)).toEqual([]);

      await close(page);
    });
  }

  /**
   * The events line wraps as text, and "ближайший" and its day are one unit
   * of it: on a 360 phone the line broke between them and the day stood alone
   * on a line of its own. The events are answered here, so the line is the
   * one that broke there whatever the seed holds: a live event whose end is
   * not known, so there is no "до", and two scheduled after it.
   */
  for (const width of CHAT_WIDTHS) {
    test(`keeps "ближайший" and its day on one line at ${width}`, async ({
      browser,
    }) => {
      const page = await openPage(browser, width, true);
      const now = Date.now();
      const DAY = 24 * 3600 * 1000;
      const events = [
        ["e2e-live", "Live", now - DAY / 24, true, "Вечер быстрых зарисовок"],
        ["e2e-next", "Scheduled", now + 2 * DAY, true, "Турнир историй"],
        ["e2e-later", "Scheduled", now + 5 * DAY, false, "Совет мастеров"],
      ].map(([id, status, starts, isOpen, title]) => ({
        id,
        status,
        isOpen,
        title,
        startsUtc: new Date(starts as number).toISOString(),
        participantCount: 1,
      }));
      await page.route(
        (url) => /\/v1\/global-chat\/events(\/e2e-[a-z]+)?$/.test(url.pathname),
        async (route) => {
          if (route.request().method() !== "GET") return route.fallback();
          const id = new URL(route.request().url()).pathname.split("/").pop();
          const one = events.find((event) => event.id === id);
          await route.fulfill({
            json: one
              ? { resource: { ...one, duration: null, participants: [] } }
              : { resources: events },
          });
        },
      );
      await page.goto("/global-chat");
      const near = page.locator(".chat-events-panel .rest-near");
      await expect(near).toBeVisible({ timeout: 15000 });
      await page.evaluate(() => document.fonts.ready);

      const tops = await near.evaluate((el) => {
        const text = el.firstChild as Text;
        const content = text.textContent ?? "";
        const topOf = (start: number, end: number) => {
          const range = document.createRange();
          range.setStart(text, start);
          range.setEnd(text, end);
          return Math.round(range.getClientRects()[0].top);
        };
        const word = content.indexOf("ближайший");
        return {
          word: topOf(word, word + "ближайший".length),
          day: topOf(content.length - 5, content.length),
        };
      });
      expect(tops.day).toBe(tops.word);

      await close(page);
    });
  }

  /**
   * The day picked in "Перейти к дате" is picked in the calendar when it
   * opens again, as it was before 5 October 2026: filled, beside today's
   * ring. A day with no message of its own is a day too: the feed opens at
   * the top of the window the server finds near it. It used to be dropped as
   * soon as it was picked, and the calendar opened on nothing picked.
   *
   * The 2nd of the month before this one: in the past whatever today is, and
   * far from the latest messages, so the feed does not stand at its end and
   * hand the reader back to them.
   */
  for (const width of CALENDAR_WIDTHS) {
    test(`shows the picked day picked when the calendar opens again at ${width}`, async ({
      browser,
    }) => {
      const page = await openPage(browser, width, true);
      await page.goto("/global-chat");
      const date = page.locator(".chat-search-row .date-btn");
      await expect(page.locator(".chat-feed .msg-avatar").first()).toBeVisible({
        timeout: 15000,
      });
      const calendar = page.locator(".chat-search-row .calendar-grid");

      // A plain visit has no day: nothing is picked, as before 5 October.
      await date.click();
      await expect(calendar).toBeVisible();
      await expect(calendar.locator(".dp-day.selected")).toHaveCount(0);
      const month = await calendar.locator(".dp-month").innerText();

      await calendar.getByRole("button", { name: "Предыдущий месяц" }).click();
      await expect(calendar.locator(".dp-month")).not.toHaveText(month);
      const before = (await calendar.locator(".dp-month").innerText()).trim();
      await calendar
        .locator(".dp-day:not([disabled])", { hasText: /^\s*2\s*$/ })
        .click();
      await expect(calendar).toBeHidden();
      await expect(page).toHaveURL(/[?&]date=\d{4}-\d{2}-02(&|$)/);
      await expect(page.locator(".chat-feed .msg-avatar").first()).toBeVisible({
        timeout: 15000,
      });
      // The window has settled: the day is still the address's.
      await page.waitForLoadState("networkidle");
      await expect(page).toHaveURL(/[?&]date=\d{4}-\d{2}-02(&|$)/);

      await date.click();
      await expect(calendar).toBeVisible();
      await expect(calendar.locator(".dp-month")).toHaveText(before);
      await expect(calendar.locator(".dp-day.selected")).toHaveText("2");

      await close(page);
    });
  }
});

/**
 * Where the date of each row of the list stands, against where it may: beside
 * the name when the name on one line, the gap and the date fit the row, and
 * under the name otherwise. Also on how many lines it is written, and whether
 * it stands before the excerpt of the last message.
 */
function datesOf(page: Page) {
  return page.locator(".chat-preview").evaluateAll((rows) =>
    rows.map((row) => {
      const content = row.querySelector<HTMLElement>(".content")!;
      const name = row.querySelector<HTMLElement>(".name-line")!;
      const date = row.querySelector<HTMLElement>(".date")!;
      const excerpt = row.querySelector<HTMLElement>(".message-preview")!;
      // The name laid out on one line, out of the flow of the row.
      const probe = name.cloneNode(true) as HTMLElement;
      probe.style.cssText =
        "position: absolute; visibility: hidden; white-space: nowrap";
      content.appendChild(probe);
      const natural = probe.getBoundingClientRect().width;
      probe.remove();
      const gap = parseFloat(getComputedStyle(content).columnGap) || 0;
      const at = date.getBoundingClientRect();
      const nameAt = name.getBoundingClientRect();
      const range = document.createRange();
      range.selectNodeContents(date);
      const lines = new Set(
        [...range.getClientRects()]
          .filter((rect) => rect.width > 0)
          .map((rect) => Math.round(rect.top)),
      ).size;
      return {
        fits: natural + gap + at.width <= content.clientWidth + 0.5,
        beside: at.top < nameAt.bottom - 1 && at.left >= nameAt.right - 0.5,
        under:
          at.top >= nameAt.bottom - 1 &&
          Math.abs(at.left - content.getBoundingClientRect().left) < 0.5,
        lines,
        beforeExcerpt: at.bottom <= excerpt.getBoundingClientRect().top + 0.5,
      };
    }),
  );
}

/** What is wrong with the place of every date of the list, if anything. */
const misplaced = (dates: Awaited<ReturnType<typeof datesOf>>) =>
  dates.filter(
    (date) =>
      date.lines !== 1 ||
      !date.beforeExcerpt ||
      (date.fits ? !date.beside : !date.under),
  );

test.describe("The correspondences on a phone", () => {
  /**
   * The date stands to the right of the name while the two fit one line, as
   * it did before 5 October 2026, and goes down whole under the name only
   * where they do not. It used to go under the last message, a third line,
   * on every row narrower than 480: on a 390 and a 430 phone too, with room
   * to spare beside an ordinary name.
   */
  for (const width of WIDTHS) {
    test(`puts the date beside the name where both fit at ${width}`, async ({
      browser,
    }) => {
      const page = await openPage(browser, width, true);
      await page.goto("/messenger");
      await expect(page.locator(".chat-preview").first()).toBeVisible({
        timeout: 15000,
      });
      await page.evaluate(() => document.fonts.ready);

      const dates = await datesOf(page);
      expect(dates.length).toBeGreaterThan(0);
      expect(misplaced(dates)).toEqual([]);

      await close(page);
    });
  }

  for (const width of WIDTHS) {
    test(`say the whole name and the whole excerpt in the list at ${width}`, async ({
      browser,
    }) => {
      const page = await openPage(browser, width, true);
      await rewrite<{
        resources?: {
          participants?: Named[];
          unreadMessagesCount?: number;
          lastMessage?: { text?: string } | null;
        }[];
      }>(page, /\/v1\/chats$/, (answer) => {
        for (const chat of answer.resources ?? []) {
          for (const one of chat.participants ?? [])
            if (one && one.username !== "TestUser") one.username = LONG;
          chat.unreadMessagesCount = 15;
          if (chat.lastMessage) chat.lastMessage.text = LONG_TEXT;
        }
      });
      await page.goto("/messenger");
      const rows = page.locator(".chat-preview");
      await expect(rows.first()).toBeVisible({ timeout: 15000 });
      await page.evaluate(() => document.fonts.ready);

      const lines = await page
        .locator(".chat-preview .name-line")
        .evaluateAll((all) =>
          all.map((line) =>
            (line as HTMLElement).innerText.replace(/\s+/g, " ").trim(),
          ),
        );
      expect(lines.length).toBeGreaterThan(0);
      expect(lines.filter((line) => line !== `${LONG} (15)`)).toEqual([]);
      const excerpts = await page
        .locator(".chat-preview .message-preview")
        .allInnerTexts();
      expect(
        excerpts.filter((text) => text.replace(/\s+/g, " ").trim() !== EXCERPT),
      ).toEqual([]);
      expect(await putAwayIn(page, ".chat-preview")).toEqual([]);
      expect(
        (await overrunOf(page, ".chat-preview .content", ".content")).filter(
          (overrun) => overrun > 0.5,
        ),
      ).toEqual([]);
      // A name of twenty wide letters leaves no room for the date beside it
      // on a phone: the date goes down whole, under the name.
      expect(misplaced(await datesOf(page))).toEqual([]);

      await close(page);
    });
  }

  /**
   * The line at the top of a correspondence says the whole name of the other
   * one, wrapping within the frame where the frame is narrower than it, and
   * nothing of the page stands past the layout of the phone.
   */
  test("says whom a correspondence is with, whole, on a 360 phone", async ({
    browser,
  }) => {
    const page = await openPage(browser, 360, true);
    await rewrite<{ participants?: Named[] }>(
      page,
      /\/v1\/chats\/[^/]+$/,
      (answer) => {
        for (const one of answer.participants ?? [])
          if (one && one.username !== "TestUser") one.username = LONG;
      },
    );
    await page.goto("/messenger");
    await page.locator(".chat-preview").first().click();
    await expect(page).toHaveURL(/\/messenger\/c\//);
    const name = page.locator(".chat-strip .line-name");
    await expect(name).toHaveText(LONG, { timeout: 15000 });
    await page.evaluate(() => document.fonts.ready);

    expect(await putAwayIn(page, ".chat-strip .line-who")).toEqual([]);
    expect(
      (await overrunOf(page, ".chat-strip", ".chat-strip")).filter(
        (overrun) => overrun > 0.5,
      ),
    ).toEqual([]);
    expect(await page.evaluate(pastTheLayout, LAYOUT_WIDTH)).toEqual([]);

    await close(page);
  });
});
