import { test as base, expect, API_BASE_URL } from "../../fixtures/auth";
import { phoneContext } from "../../fixtures/phone";
import type {
  APIRequestContext,
  BrowserContext,
  Locator,
  Page,
} from "@playwright/test";

/**
 * The text of a post, of a comment, of a topic and of a record of the log
 * stands in DM2's lines, the same in all of them. DM2 set every text block
 * in 16px on a step of 20, the post, the comment and the ban log alike. A
 * paragraph there is the blank line its author left, so two paragraphs stand
 * 40 apart, top to top. The brown text of a post stands 33px under its game
 * text, farther than a blank line, so it reads as a word apart.
 *
 * Each text is put into the page through the answer of its request, so the
 * lines read are known whatever the seed writes: a paragraph long enough for
 * several lines at every width, a blank line, a second paragraph, and the
 * brown text of a post. What is read is where the lines stand: the step from
 * one line to the next inside the paragraph, the step from the last line of
 * the first paragraph to the first line of the second, and the air from the
 * line box of the last line of the game text to the line box of the first
 * line of the brown text.
 *
 * Read in a window of 1366 and on a phone, as a phone lays the page out
 * (fixtures/phone.ts). The chat keeps its own lines and is not read here.
 */
const SCREENS = [1366, "phone"] as const;
type Screen = (typeof SCREENS)[number];

const test = base.extend<{
  /** A page on a phone or in a window of a width, closed after the test. */
  pageOn: (screen: Screen) => Promise<Page>;
}>({
  pageOn: async ({ browser }, use) => {
    const contexts: BrowserContext[] = [];
    await use(async (screen) => {
      const context =
        screen === "phone"
          ? await phoneContext(browser)
          : await browser.newContext({
              viewport: { width: screen, height: 900 },
            });
      contexts.push(context);
      return context.newPage();
    });
    for (const context of contexts) await context.close();
  },
});

/** DM2's step of a line of text, 16px set in "line-height: normal". */
const LINE = 20;

/** A blank line between two paragraphs, top to top. */
const PARAGRAPH = 2 * LINE;

/** DM2's air between the game text of a post and its brown text. */
const BROWN_GAP = 33;

const FIRST =
  "Джек смеется и хлопает боцмана по плечу. Кошелек боцмана уже у него, " +
  "и пока команда горланит песни, он прячет добычу за пазуху и незаметно " +
  "отходит к трапу. Там он поворачивается и объявляет, что ром сегодня за его счет.";
const SECOND = "Команда отвечает дружным ревом, до рассвета еще далеко.";
const TEXT = `${FIRST}<br /><br />${SECOND}`;
const BROWN = "Это было прекрасно. У меня слезы на глазах.";

/** A seeded game whose first ordinary room a guest reads, with posts. */
const GAME_TITLE = "Хроники Забытых Королевств";

/** A seeded topic with comments, open to a guest. */
const TOPIC = "/forum/general/5";

type Resource = Record<string, unknown>;
type Answer = { resource?: Resource; resources?: Resource[] };

/**
 * Hands the page the answer of a GET whose path matches, with one change made
 * to it. Nothing is written: every other request goes on as it was. An answer
 * the change finds nothing in goes on unchanged, and the text the test waits
 * for never shows.
 */
async function substitute(
  page: Page,
  path: RegExp,
  change: (answer: Answer) => void,
) {
  await page.route(
    (url) => path.test(url.pathname),
    async (route) => {
      if (route.request().method() !== "GET") return route.fallback();
      const response = await route.fetch();
      const answer = (await response.json()) as Answer;
      change(answer);
      await route.fulfill({ response, json: answer });
    },
  );
}

/** The address of the first ordinary room of the seeded game. */
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

async function open(page: Page, path: string, ready: Locator) {
  await page.goto(path);
  await expect(ready).toBeVisible({ timeout: 15000 });
  await page.evaluate(() => document.fonts.ready);
}

interface Lines {
  /** The tops of the lines of each paragraph: a paragraph is a text node. */
  paragraphs: number[][];
  /** The computed line height of the block, in px. */
  lineHeight: number;
}

/** Where the lines of a block of text stand. */
function linesOf(block: Locator): Promise<Lines> {
  return block.evaluate((element) => {
    const paragraphs: number[][] = [];
    const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      if (!node.textContent?.trim()) continue;
      const range = document.createRange();
      range.selectNodeContents(node);
      const tops = [...range.getClientRects()]
        .filter((r) => r.width > 0.5 && r.height > 0)
        .map((r) => Math.round(r.top * 100) / 100);
      paragraphs.push([...new Set(tops)].sort((a, b) => a - b));
    }
    return {
      paragraphs,
      lineHeight: parseFloat(getComputedStyle(element).lineHeight),
    };
  });
}

const round = (value: number) => Math.round(value * 100) / 100;

/** The steps between the lines of one paragraph. */
const steps = (tops: number[]) =>
  tops.slice(1).map((top, i) => round(top - tops[i]));

/**
 * A block of text in DM2's lines: two paragraphs, the first on several lines
 * a step of 20 apart, the second a blank line under it.
 */
async function expectDm2Lines(name: string, block: Locator): Promise<Lines> {
  const lines = await linesOf(block);
  const [one, two] = lines.paragraphs;
  expect(lines.paragraphs.length, `${name}: two paragraphs`).toBe(2);
  expect(one.length, `${name}: the first paragraph on lines`).toBeGreaterThan(
    1,
  );
  const off = steps(one).filter((step) => Math.abs(step - LINE) > 0.5);
  expect
    .soft(off, `${name}: the steps of a line that are not ${LINE}`)
    .toEqual([]);
  const blank = round(two[0] - one[one.length - 1]);
  expect
    .soft(
      Math.abs(blank - PARAGRAPH),
      `${name}: a blank line is ${blank}, not ${PARAGRAPH}`,
    )
    .toBeLessThanOrEqual(1);
  return lines;
}

test.describe("Text in the lines of DM2", () => {
  for (const screen of SCREENS) {
    const where = screen === "phone" ? "on a phone" : `at ${screen}px`;
    test(`a post ${where}: a line of ${LINE}, a blank line of ${PARAGRAPH}, the brown text ${BROWN_GAP} under the game text`, async ({
      pageOn,
      request,
    }) => {
      const page = await pageOn(screen);
      await substitute(page, /\/v1\/rooms\/[^/]+\/posts$/, (answer) => {
        const post = answer.resources?.[0];
        if (!post) return;
        post.gameText = TEXT;
        post.metagameText = BROWN;
        // Rolls stand between the two blocks and the brown text then stands
        // on their 22: the gap measured here is the one with no rolls.
        post.diceRolls = [];
      });
      const text = page.locator("main#main .game-text", { hasText: SECOND });
      const brown = page.locator("main#main .metagame-text", {
        hasText: BROWN,
      });
      await open(page, await roomPath(request), text);

      const game = await expectDm2Lines("the game text", text);
      const meta = await linesOf(brown);
      const last = game.paragraphs[game.paragraphs.length - 1];
      expect(meta.paragraphs.length, "the brown text").toBe(1);
      expect
        .soft(meta.lineHeight, "the line of the brown text")
        .toBe(game.lineHeight);
      // From the line box of the last line of the game text to the line box
      // of the first line of the brown text: the tops of the two less a line.
      const gap = round(
        meta.paragraphs[0][0] - last[last.length - 1] - game.lineHeight,
      );
      expect(
        Math.abs(gap - BROWN_GAP),
        `the brown text stands ${gap} under the game text, not ${BROWN_GAP}`,
      ).toBeLessThanOrEqual(1);
    });

    test(`a topic and its comment ${where}: a line of ${LINE}, a blank line of ${PARAGRAPH}`, async ({
      pageOn,
    }) => {
      const page = await pageOn(screen);
      await substitute(page, /\/v1\/forum\/general\/5$/, (answer) => {
        if (answer.resource) answer.resource.description = TEXT;
      });
      await substitute(page, /\/v1\/topics\/[^/]+\/comments$/, (answer) => {
        const comment = answer.resources?.[0];
        if (comment) comment.text = TEXT;
      });
      const topic = page.locator("main#main .topic-text", { hasText: SECOND });
      const comment = page.locator("main#main .comment-text", {
        hasText: SECOND,
      });
      await open(page, TOPIC, comment);
      await expect(topic).toBeVisible();

      await expectDm2Lines("the text of the topic", topic);
      await expectDm2Lines("the text of the comment", comment);
    });

    test(`a record of the log of warnings ${where}: a line of ${LINE}, a blank line of ${PARAGRAPH}`, async ({
      pageOn,
    }) => {
      const page = await pageOn(screen);
      // A warning about a comment, whose text the record quotes in its text
      // line however the log draws the other kinds. Not the first record of
      // the log: which one that is depends on the seed, and a warning about a
      // profile, the first one there now, is the kind the log is to show as a
      // snapshot of the profile rather than as a text.
      await substitute(page, /\/v1\/warnings$/, (answer) => {
        const warning = answer.resources?.find(
          (row) => row.entityType === "Comment",
        );
        if (warning) warning.evidence = TEXT;
      });
      const record = page.locator("main#main .record-block__text", {
        hasText: SECOND,
      });
      await open(page, "/warnings", record);

      await expectDm2Lines("the text of the record", record);
    });
  }
});
