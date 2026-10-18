import { test, expect, API_BASE_URL } from "../../fixtures/auth";
import { PHONES, phoneContext } from "../../fixtures/phone";
import type { APIRequestContext, Browser, Page } from "@playwright/test";

/**
 * A post and a comment on a phone are the post and the comment of the full
 * version, pictures of the same size: a phone has no layout of its own.
 *
 * The left column of a post stands beside its text, as on 1366 and as on DM2:
 * 140 wide, the portrait 128 wide in it. The rows of a comment wrap between
 * their parts: the name, the letter of the role and [offline] all stand, the
 * name whole, never cut under "…" with the rest of it in a tooltip, and
 * nothing goes under "Еще".
 *
 * Read as a guest. The names of a topic's comments are replaced in the answer
 * on its way to the page, nothing is written: the first comment gets the
 * longest login of the seed, the second a login of twenty wide letters, the
 * longest a login can be (UsernamePolicy), wider than the row of a phone.
 */

/** The longest login of the seed, and one of twenty wide letters. */
const LOGINS = ["LongestLoginPossible", "Ш".repeat(20)];

/** A date and its time as the site writes them: "28.09.2026 в 13:41". */
const DATE_TIME = /\d{2}\.\d{2}\.\d{4}\s+в\s+\d{1,2}:\d{2}/;

/** The address of a topic with two comments at least, read off the API. */
async function topicWithComments(request: APIRequestContext): Promise<string> {
  const boards = await request.get(`${API_BASE_URL}/v1/boards`);
  expect(boards.ok()).toBe(true);
  const { resources = [] } = (await boards.json()) as {
    resources?: { alias: string }[];
  };
  for (const { alias } of resources) {
    const topics = await request.get(
      `${API_BASE_URL}/v1/boards/${alias}/topics?take=20`,
    );
    if (!topics.ok()) continue;
    const answer = (await topics.json()) as {
      resources?: { topicNumber: number; commentsCount: number }[];
    };
    const topic = (answer.resources ?? []).find(
      (one) => one.commentsCount >= 2,
    );
    if (topic) return `/forum/${alias}/${topic.topicNumber}`;
  }
  throw new Error("no topic of the stand has two comments");
}

/**
 * Opens the topic at a width in a layout of comments, the names of its
 * comments replaced by the logins above, one each.
 */
async function openTopic(
  browser: Browser,
  address: string,
  width: number,
  layout: "full" | "compact",
): Promise<Page> {
  const context = await phoneContext(browser, { width });
  await context.addInitScript((value) => {
    localStorage.setItem("dm_message_layout", value);
  }, layout);
  const page = await context.newPage();
  await page.route(
    (url) => /\/v1\/topics\/[^/]+\/comments$/.test(url.pathname),
    async (route) => {
      if (route.request().method() !== "GET") return route.fallback();
      const response = await route.fetch();
      const answer = (await response.json()) as {
        resources?: { author?: { username?: string } | null }[];
      };
      (answer.resources ?? []).slice(0, LOGINS.length).forEach((one, i) => {
        if (one.author) one.author.username = LOGINS[i];
      });
      await route.fulfill({ response, json: answer });
    },
  );
  await page.goto(address);
  await expect(page.locator("div.comment").nth(1)).toBeVisible({
    timeout: 15000,
  });
  await page.evaluate(() => document.fonts.ready);
  return page;
}

type Seen = {
  /** The name as it stands, and whether all of it is drawn. */
  name: string;
  /** The box that clips the name short of its text, if any. */
  clippedBy: string | null;
  /** How far the farthest letter of the comment stands past its edge. */
  overrun: number;
  /** Elements in the comment that cut their text with "…". */
  cut: string[];
  /** Whether the name carries a tooltip of itself. */
  titled: boolean;
  /** Hidden elements anywhere in the comment, and menus that hold what left a row. */
  putAway: number;
  /** The row of the name, or the caption in the compact layout. */
  row: string;
  /** The date in the author's column of the full layout, the caption in the compact. */
  dates: string;
};

/** The first two comments, as laid out, by the selector of the name. */
function readComments(page: Page, nameSelector: string): Promise<Seen[]> {
  return page.evaluate((selector) => {
    const comments = [
      ...document.querySelectorAll<HTMLElement>("div.comment"),
    ].slice(0, 2);
    return comments.map((comment) => {
      const edge = comment.getBoundingClientRect().right;
      const name = comment.querySelector<HTMLElement>(selector);
      /** The boxes of the letters of an element, out-of-flow boxes aside. */
      const letters = (root: Element): DOMRect[] => {
        const rects: DOMRect[] = [];
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
        for (let node = walker.nextNode(); node; node = walker.nextNode()) {
          if (!node.textContent?.trim()) continue;
          const owner = node.parentElement;
          if (owner && getComputedStyle(owner).position === "absolute")
            continue;
          const range = document.createRange();
          range.selectNodeContents(node);
          for (const rect of range.getClientRects())
            if (rect.width > 0) rects.push(rect);
        }
        return rects;
      };
      let clippedBy: string | null = null;
      if (name) {
        const own = letters(name);
        const right = Math.max(...own.map((rect) => rect.right));
        for (
          let box: HTMLElement | null = name;
          box && box !== comment.parentElement;
          box = box.parentElement
        ) {
          const style = getComputedStyle(box);
          const clips =
            style.overflowX !== "visible" || style.textOverflow === "ellipsis";
          if (clips && box.getBoundingClientRect().right < right - 0.5) {
            clippedBy = box.className || box.tagName;
            break;
          }
        }
      }
      const rows = comment.querySelector(".comment-header");
      const caption = comment.querySelector(".comment-footer .caption-line");
      const text = (element: Element | null) =>
        (element as HTMLElement | null)?.innerText
          .replace(/\s+/g, " ")
          .trim() ?? "";
      return {
        name: name?.innerText.replace(/\s+/g, "") ?? "",
        clippedBy,
        overrun: Math.max(...letters(comment).map((rect) => rect.right)) - edge,
        cut: [comment, ...comment.querySelectorAll("*")]
          .filter((el) => getComputedStyle(el).textOverflow === "ellipsis")
          .map((el) => el.textContent ?? ""),
        titled: !!name?.hasAttribute("title"),
        putAway: comment.querySelectorAll("[hidden], .more-menu").length,
        row: text(rows?.querySelector(".author-line") ?? caption),
        dates: text(rows?.querySelector(".comment-date") ?? caption),
      };
    });
  }, nameSelector);
}

test.describe("A post and a comment on a phone", () => {
  for (const width of PHONES) {
    test(`stands the column of the best post beside its text at ${width}, the portrait 128 wide`, async ({
      browser,
    }) => {
      const context = await phoneContext(browser, { width });
      const page = await context.newPage();
      await page.goto("/");
      const portrait = page.locator(".post-card .avatar").first();
      await expect(portrait).toBeVisible({ timeout: 15000 });
      await page.evaluate(() => document.fonts.ready);

      const layout = await portrait.evaluate((image) => {
        const card = image.closest(".post-card")!;
        const box = (selector: string) =>
          card.querySelector(selector)!.getBoundingClientRect();
        return {
          portrait: image.getBoundingClientRect().width,
          column: box(".post-meta").width,
          columnRight: box(".post-meta").right,
          columnTop: box(".post-meta").top,
          textLeft: box(".post-body").left,
          textTop: box(".post-body").top,
        };
      });
      expect(layout.portrait).toBeCloseTo(128, 0);
      expect(layout.column).toBeCloseTo(140, 0);
      // The text stands beside the column, 10 to the right of it, level with
      // its top.
      expect(layout.textLeft).toBeCloseTo(layout.columnRight + 10, 0);
      expect(layout.textTop).toBeCloseTo(layout.columnTop, 0);

      await context.close();
    });

    test(`keeps every part of a comment at ${width}, the name whole`, async ({
      browser,
      request,
    }) => {
      const address = await topicWithComments(request);
      for (const layout of ["full", "compact"] as const) {
        const page = await openTopic(browser, address, width, layout);
        const seen = await readComments(
          page,
          layout === "full"
            ? ".comment-header .author-name"
            : ".comment-footer .author-link",
        );

        expect(seen.map((one) => one.name)).toEqual(LOGINS);
        for (const one of seen) {
          const where = `${layout} ${width}: ${one.row}`;
          expect(one.clippedBy, where).toBeNull();
          expect(one.titled, where).toBe(false);
          expect(one.cut, where).toEqual([]);
          expect(one.putAway, where).toBe(0);
          expect(one.overrun, where).toBeLessThanOrEqual(0.5);
          // The mark of presence and the date with its time are in the rows.
          expect(one.row, where).toMatch(/\[(online|offline)\]/);
          expect(one.dates, where).toMatch(DATE_TIME);
          expect(`${one.row} ${one.dates}`, where).not.toMatch(/Еще|…/);
        }
        await page.context().close();
      }
    });
  }
});
