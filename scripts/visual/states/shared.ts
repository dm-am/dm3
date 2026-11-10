import type { Page } from "@playwright/test";
import type { Fixture } from "../fixture";
import type { RoleName } from "../roles";

/**
 * One thing the stand looks at: who looks, where, and what is done on the page
 * before it is captured.
 *
 * Pages are reached the way a reader reaches them - by address, by the visible
 * name of a button or a link - and not by class, so a clean-up that renames a
 * class still reaches the same state, and the frames say what that clean-up
 * changed instead of failing to open the page.
 */
export interface State {
  name: string;
  role: RoleName;
  url: string | ((fixture: Fixture) => string);
  /** What is done before the capture. */
  act?: (page: Page) => Promise<void>;
  /**
   * Whether the window is stretched to the height of the page before the
   * capture, so the frame holds the whole page and not its first screen.
   * Off for the states of something that floats (a dialog, a menu, a picker):
   * those are about where they open in a window of the real size.
   */
  whole?: boolean;
  /** Entries of localStorage beyond the session and the theme. */
  storage?: Record<string, string>;
  /** The instant the page's clock stands at, when not the seed epoch. */
  clock?: (fixture: Fixture) => string;
}

/** A button by its whole visible name, or by a pattern of it. */
export const button = (page: Page, name: string | RegExp) =>
  page
    .getByRole("button", {
      name,
      exact: typeof name === "string" ? true : undefined,
    })
    .first();

/** A token the server knows none such. */
export const DEAD = "0000dead0000dead0000dead0000dead";

/** The dead token where the letter pages read it: in the fragment. */
export const DEAD_TOKEN = `#token=${DEAD}`;

/** The guest ticket of the seed, whose tracking token is fixed (DataSeeder.Tickets.cs). */
export const GUEST_TICKET_TOKEN = "5eedc0de5eedc0de5eedc0de5eedc0de";

/** A finished game whose first post carries an attachment, two rolls and reviews (DataSeeder.GamePosts.cs, the reviews DataSeeder.Showcase.cs). */
export const BIG_POST_GAME = "aaabp";

/**
 * The finished game whose figure's note carries a map marked for Бран Молчун and a
 * letter for every reader (DataSeeder.HiddenGameContent.cs); no role plays in it.
 */
export const HIDDEN_GAME = "aaadb";

/** The game the member owns, and the account removed by the seed. */
export const OWNED_GAME = "aaaam";
export const REMOVED_ACCOUNT = "Ушедший Мастер";

/** The game the administrator masters: pendency, five rooms, a declined character (DataSeeder.Games.cs). */
export const ADMIN_GAME = "aaaaa";

/** Its declined character, by the id the seed fixes, since no list shows one (DataSeeder.Characters.cs). */
export const DECLINED_CHARACTER = "00000000-0000-0000-00c1-000000000001";

/** A public draft of Experienced without characters, whose sheet saves straight away. */
export const DRAFT_GAME = "aaabh";

/** Some markup typed into the comment editor of a topic, in its BBCode mode. */
export async function writeBbcode(page: Page) {
  await button(page, "Режим BBCode").click();
  await page
    .getByRole("textbox", { name: "Написать комментарий..." })
    .fill("[b]Жирный[/b] и [i]курсив[/i]\n[quote]Цитата[/quote]");
}

export async function openFilter(page: Page) {
  await button(page, "Фильтры").click();
}

/**
 * Answers one method on one address the way the API refuses, with a problem
 * document whose title is the sentence the page shows, and lets every other
 * request through. The refusal never reaches the server, so a state that
 * shows how a page takes a refusal writes nothing. Addresses are regular
 * expressions: a glob reads the "?" of a query string as a wildcard.
 */
export async function refuse(
  page: Page,
  url: RegExp,
  method: string,
  status: number,
  title: string,
) {
  await page.route(url, (route) =>
    route.request().method() === method
      ? route.fulfill({
          status,
          contentType: "application/problem+json",
          body: JSON.stringify({ type: "about:blank", title, status }),
        })
      : route.fallback(),
  );
}

/** A page whose read fails on load: the read is refused before the page loads again. */
export async function refuseOnLoad(page: Page, url: RegExp) {
  await refuse(page, url, "GET", 500, "Ошибка");
  await page.reload({ waitUntil: "load" });
}

/** Answers GET on one address with the given body, for a list the seed cannot hold for long. */
export async function answer(page: Page, url: RegExp, body: unknown) {
  await page.route(url, (route) =>
    route.request().method() === "GET"
      ? route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify(body),
        })
      : route.fallback(),
  );
}

/**
 * Hovers the matching elements one after another until a tooltip with the
 * text opens: which of them has one is a draw of the seed.
 */
export async function hoverUntilTooltip(
  page: Page,
  selector: string,
  text: string,
) {
  const targets = page.locator(selector);
  const count = await targets.count();
  for (let i = 0; i < count; i++) {
    await targets.nth(i).hover();
    try {
      await page
        .getByRole("tooltip")
        .filter({ hasText: text })
        .waitFor({ timeout: 800 });
      return;
    } catch {
      // Not this one: the next.
    }
  }
  throw new Error(`no tooltip with "${text}" over ${count} of ${selector}`);
}

/** A like the reader may press with a count above zero, by its words "Нравится (3)". */
export const LIKED = 'button:text-matches("^Нравится [(][0-9]+[)]$")';

/** A section of the settings of a game or a blog, by its heading. */
export const settingsSection = (page: Page, title: string) =>
  page.locator("section.settings-section", { hasText: title }).first();

/** A section of the account page, by its heading. */
export const accountSection = (page: Page, title: string) =>
  page
    .locator("section", {
      has: page.getByRole("heading", { name: title, exact: true }),
    })
    .first();

/**
 * Fills the login dialog with an address and the seed's password, sends it and
 * waits for the answer: the form holds the request back until three seconds
 * after it opened (useBotTrap), so the page is quiet before it goes out.
 */
export async function signIn(page: Page, email: string) {
  await page.getByRole("textbox", { name: /почт/i }).fill(email);
  await page.locator('input[type="password"]').fill("Test123!");
  const answered = page.waitForResponse((response) =>
    /\/v1\/account\/login$/.test(response.url()),
  );
  // The dialog's own button: the header has one of the same name.
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Войти", exact: true })
    .click();
  await answered;
}

/** A picture of one pixel: enough to be a file the composer picked. */
export const PNG_1X1 = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
  "base64",
);

/** "карта.png" chosen in the form of a post: a file the form holds, never sent. */
export async function chooseComposerFile(page: Page) {
  await page.locator(".attachments input[type=file]").setInputFiles({
    name: "карта.png",
    mimeType: "image/png",
    buffer: PNG_1X1,
  });
}

/** A moment some minutes past another, for the clock of a state. */
export const minutesAfter = (moment: string, minutes: number) =>
  new Date(Date.parse(moment) + minutes * 60_000).toISOString();
