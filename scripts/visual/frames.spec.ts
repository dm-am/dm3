import { test, type Browser, type Page } from "@playwright/test";
import { readFileSync } from "fs";
import { join } from "path";
import { captureState, pinRandomness, settle } from "./capture";
import type { Fixture } from "./fixture";
import { masksFor } from "./masks";
import { COMMON_KEYS, STATES, type Project, type State } from "./states";
import { addressOf, UNORDERED } from "./unordered.mjs";
import { collapseRepeated, REPEATED } from "./api-volatile.mjs";

const AUTH = process.env.VISUAL_AUTH!;
const EPOCH = process.env.VISUAL_EPOCH!;
const THEME = process.env.VISUAL_THEME === "dark" ? "dark" : "light";

/** Hosts the page may reach: the build and the stack. Nothing on the internet. */
const LOCAL = /^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?\//;

/**
 * The tallest window a frame is taken in, in CSS pixels. A phone frame is
 * three device pixels to one, and a frame much taller than this is beyond what
 * the browser rasterises in one piece; the rest of such a page is still in the
 * tree and the styles.
 */
const TALLEST: Record<Project, number> = { desktop: 12_000, phone: 4_000 };

const fixture = (): Fixture =>
  JSON.parse(readFileSync(join(AUTH, "fixture.json"), "utf8"));

async function open(browser: Browser, state: State, project: Project) {
  const context = await browser.newContext({
    ...(project === "phone"
      ? {
          viewport: { width: 390, height: 844 },
          deviceScaleFactor: 3,
          isMobile: true,
          hasTouch: true,
        }
      : { viewport: { width: 1920, height: 911 }, deviceScaleFactor: 1 }),
    locale: "ru-RU",
    timezoneId: "Europe/Moscow",
    colorScheme: THEME,
    storageState:
      state.role === "guest" ? undefined : join(AUTH, `${state.role}.json`),
  });
  // A 429 is the API's rate limiting and not the page: a frame of its
  // refusals would go into the base as the page, or into a report as a change.
  const refused: string[] = [];
  context.on("response", (response) => {
    if (response.status() === 429) refused.push(response.url());
  });
  // The pings of the site's other addresses go to the internet; refused, they
  // read "нет" on every run instead of a latency of the moment.
  await context.route("**/*", (route) =>
    LOCAL.test(route.request().url())
      ? route.continue()
      : route.abort("internetdisconnected"),
  );
  // The lists the API returns in no order reach the page in the order the API
  // snapshot compares them in (unordered.mjs): otherwise a list drawn as the
  // server happened to return it differs between two runs of one commit.
  const runWritten = (moment: unknown) =>
    typeof moment === "string" &&
    Date.parse(moment) > Date.parse(EPOCH) + 24 * 60 * 60_000;
  for (const rule of UNORDERED) {
    await context.route(addressOf(rule.route), async (route) => {
      if (route.request().method() !== "GET") return route.fallback();
      const response = await route.fetch();
      if (!response.ok()) return route.fulfill({ response });
      const json = await response.json();
      rule.order(json, runWritten);
      await route.fulfill({ response, json });
    });
  }
  // The rows every sign-in of a stand's run adds (sessions, the security
  // log) reach the page folded as the API snapshot folds them
  // (api-volatile.mjs): otherwise each run lists one row more than the last.
  for (const rule of REPEATED) {
    await context.route(addressOf(rule.route), async (route) => {
      if (route.request().method() !== "GET") return route.fallback();
      const response = await route.fetch();
      if (!response.ok()) return route.fulfill({ response });
      const json = await response.json();
      collapseRepeated(json, rule, runWritten);
      await route.fulfill({ response, json });
    });
  }
  await context.addInitScript(pinRandomness);
  await context.addInitScript(
    (entries) => {
      for (const [key, value] of Object.entries(entries))
        localStorage.setItem(key, value);
    },
    {
      dm_theme: THEME,
      // The browser's zone is not the account's, and the interface asks once
      // per device whether to align them; answered here, so the question is a
      // state of its own instead of a toast over every other.
      "dm3-time-zone-asked": "Europe/Moscow",
      ...state.storage,
    },
  );
  const page = await context.newPage();
  const instant = state.clock ? state.clock(fixture()) : EPOCH;
  await page.clock.setFixedTime(new Date(instant));
  return { page, refused };
}

function refuseRateLimited(state: State, refused: string[]) {
  if (refused.length === 0) return;
  throw new Error(
    `${state.name}: the API answered 429 to ${refused[0]}: its rate limiting is on, and the page shows the refusal; ` +
      '"powershell -File scripts/dm.ps1 e2e" recreates only the API, with the limits off, and the data stays',
  );
}

/**
 * Stretches the window to the height of what scrolls: the page scrolls inside
 * its own container rather than the document, so a full-page capture alone
 * would hold only the first screen. Repeated, because a taller window can
 * bring in more (a list that loads the next part when its end comes into view).
 */
async function stretch(page: Page, project: Project) {
  for (let round = 0; round < 3; round++) {
    const size = page.viewportSize()!;
    const { overflow, scale } = await page.evaluate(() => {
      let most = Math.max(
        0,
        document.documentElement.scrollHeight - innerHeight,
      );
      for (const element of document.querySelectorAll("*")) {
        if (element.clientHeight < innerHeight / 2) continue;
        if (!/(auto|scroll)/.test(getComputedStyle(element).overflowY))
          continue;
        most = Math.max(most, element.scrollHeight - element.clientHeight);
      }
      return { overflow: most, scale: innerHeight };
    });
    if (overflow <= 0) return;
    // innerHeight is in layout pixels, which a phone scales to its screen.
    const height = Math.min(
      TALLEST[project],
      Math.ceil(size.height + (overflow * size.height) / scale),
    );
    if (height === size.height) return;
    await page.setViewportSize({ width: size.width, height });
    await settle(page);
  }
}

for (const state of STATES) {
  test(state.name, async ({ browser }, info) => {
    const project = info.project.name as Project;
    test.skip(
      !!state.projects && !state.projects.includes(project),
      `${state.name} is not a state of ${project}`,
    );

    const { page, refused } = await open(browser, state, project);
    const url =
      typeof state.url === "function" ? state.url(fixture()) : state.url;
    await page.goto(url, { waitUntil: "load" });
    await settle(page);
    if (state.whole !== false) await stretch(page, project);
    if (state.act) {
      await state.act(page);
      await settle(page);
    }
    // A state that signs its account out (as "Выйти везде" once did) turns
    // every later state of that account into a guest's page, which would be
    // taken into the base without a word. Refused here instead.
    if (
      state.role !== "guest" &&
      (await page.getByTestId("user-menu").count()) === 0
    ) {
      throw new Error(
        `${state.name}: the page is a guest's, the session of ${state.role} is gone`,
      );
    }
    refuseRateLimited(state, refused);
    await captureState(
      page,
      info,
      state.name,
      COMMON_KEYS,
      masksFor(state.name),
    );
    refuseRateLimited(state, refused);
    await page.context().close();
  });
}
