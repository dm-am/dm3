import { test, expect } from "../../fixtures/auth";
import { PHONES, phoneContext } from "../../fixtures/phone";
import type { Browser, Page } from "@playwright/test";

/**
 * The log of warnings on a phone is the log of the full version, never
 * shortened: a phone lays the page out at the width of a narrow window and the
 * reader zooms in (fixtures/phone.ts). The caption of a record and the caption
 * of each decision under it wrap between their parts, as they do on a desktop:
 * what was decided, by whom, and the date with its time all stand in the line.
 * Nothing goes under "Еще", no name is cut, and nothing runs past the edge of
 * the record.
 *
 * The open log is read as a guest. Every name in its answer is replaced by a
 * login of twenty wide letters, the longest a login can be (UsernamePolicy), so
 * that the captions are the longest the log can hold whatever the seed writes.
 * Nothing is written: the answer of the GET is changed on its way to the page.
 */

/** A login of twenty wide letters. */
const LONG = "Ш".repeat(20);

/** A date and its time as the site writes them: "08.10.2026 в 09:41". */
const DATE_TIME = /\d{2}\.\d{2}\.\d{4}\s+в\s+\d{1,2}:\d{2}/;

type Person = { username?: string } | null | undefined;
type Entry = {
  user?: Person;
  moderator?: Person;
  pointsChangedBy?: string | null;
  entityCreatedUtc?: string | null;
};

/**
 * Opens the open log as a guest at a width, every name in it the long login.
 * Returns the page and how many records of the answer have a dated text.
 */
async function openLog(
  browser: Browser,
  width: number,
): Promise<{ page: Page; dated: number }> {
  const context = await phoneContext(browser, { width });
  const page = await context.newPage();
  let dated = 0;
  await page.route(
    (url) => /\/v1\/warnings$/.test(url.pathname),
    async (route) => {
      if (route.request().method() !== "GET") return route.fallback();
      const response = await route.fetch();
      const answer = (await response.json()) as { resources?: Entry[] };
      const entries = answer.resources ?? [];
      for (const entry of entries) {
        if (entry.user) entry.user.username = LONG;
        if (entry.moderator) entry.moderator.username = LONG;
        if (entry.pointsChangedBy) entry.pointsChangedBy = LONG;
      }
      dated = entries.filter((entry) => entry.entityCreatedUtc).length;
      await route.fulfill({ response, json: answer });
    },
  );
  await page.goto("/warnings");
  await expect(page.locator(".warning-card").first()).toBeVisible({
    timeout: 15000,
  });
  await page.evaluate(() => document.fonts.ready);
  return { page, dated };
}

type Caption = {
  /** The text as it stands on the page: what is hidden is not in it. */
  text: string;
  /** Whether it is the caption of a decision, and not of the record. */
  decision: boolean;
  /** Elements in it that cut their text with "…". */
  cut: string[];
  /** Hidden elements in it, and menus that hold what left the line. */
  putAway: number;
  /** How far its farthest letter stands past the edge of the record. */
  overrun: number;
};

/**
 * Every caption of the log, as laid out. The letters are read as text ranges,
 * not as boxes, so the bar of a gap that the line broke at, a box taken out of
 * the flow and clipped to nothing, counts for nothing.
 */
function readCaptions(page: Page): Promise<Caption[]> {
  return page.evaluate(() => {
    const selector = ".record-block__caption, .record-verdict__caption";
    return [...document.querySelectorAll<HTMLElement>(selector)].map(
      (caption) => {
        const record = caption.closest(".warning-card") ?? caption;
        const edge = record.getBoundingClientRect().right;
        const elements = [caption, ...caption.querySelectorAll("*")];
        let far = -Infinity;
        const walker = document.createTreeWalker(caption, NodeFilter.SHOW_TEXT);
        for (let node = walker.nextNode(); node; node = walker.nextNode()) {
          if (!node.textContent?.trim()) continue;
          const owner = node.parentElement;
          if (owner && getComputedStyle(owner).position === "absolute")
            continue;
          const range = document.createRange();
          range.selectNodeContents(node);
          for (const rect of range.getClientRects())
            if (rect.width > 0) far = Math.max(far, rect.right);
        }
        return {
          text: caption.innerText.replace(/\s+/g, " ").trim(),
          decision: caption.matches(".record-verdict__caption"),
          cut: elements
            .filter((el) => getComputedStyle(el).textOverflow === "ellipsis")
            .map((el) => el.textContent ?? ""),
          putAway: caption.querySelectorAll("[hidden], .more-menu").length,
          overrun: far - edge,
        };
      },
    );
  });
}

test.describe("The log of warnings on a phone", () => {
  for (const width of PHONES) {
    test(`wraps every caption at ${width}, the date and its time in each decision`, async ({
      browser,
    }) => {
      const { page, dated } = await openLog(browser, width);
      const captions = await readCaptions(page);
      const decisions = captions.filter((caption) => caption.decision);
      const records = captions.filter((caption) => !caption.decision);

      expect(decisions.length).toBeGreaterThan(0);
      // Every decision is dated, and says by whom, the whole name.
      expect(
        decisions
          .filter((caption) => !DATE_TIME.test(caption.text))
          .map((caption) => caption.text),
      ).toEqual([]);
      expect(
        decisions
          .filter((caption) => !caption.text.includes(` от ${LONG},`))
          .map((caption) => caption.text),
      ).toEqual([]);
      // A record whose text is dated says when, with the time.
      expect(
        records.filter((caption) => DATE_TIME.test(caption.text)),
      ).toHaveLength(dated);
      expect(
        captions
          .filter((caption) => /Еще|…/.test(caption.text))
          .map((caption) => caption.text),
      ).toEqual([]);
      expect(captions.flatMap((caption) => caption.cut)).toEqual([]);
      expect(
        captions
          .filter((caption) => caption.putAway > 0)
          .map((caption) => caption.text),
      ).toEqual([]);
      expect(
        captions
          .filter((caption) => caption.overrun > 0.5)
          .map((caption) => `${caption.text}: ${caption.overrun}`),
      ).toEqual([]);

      await page.context().close();
    });
  }
});
