import {
  test as base,
  expect,
  request as playwrightRequest,
  type Page,
  type APIRequestContext,
} from "@playwright/test";
import { fileURLToPath } from "url";
import { dirname, join } from "path";

export const API_BASE_URL = process.env.VITE_API_URL || "http://localhost:5000";

/**
 * Port of the preview server the suite runs against.
 *
 * Not a free choice: the API validates Origin twice (CORS allowlist and the
 * CSRF middleware), and its allowlist holds 5173, 5174 and 8080 only. On any
 * other port every request from the page is rejected and the whole tier reads
 * red for a reason that has nothing to do with the tests.
 */
export const PREVIEW_PORT = 5174;

/**
 * The address the browser opens. Declared here rather than in the config
 * because global setup writes localStorage for exactly this origin, and two
 * copies of the address are two chances for the session to be saved against a
 * host the page never visits.
 *
 * An external target under test (staging, a container, a manually started
 * server) comes through E2E_BASE_URL and is used as-is.
 */
export const APP_BASE_URL =
  process.env.E2E_BASE_URL || `http://localhost:${PREVIEW_PORT}`;

/**
 * Seeded dev accounts. Not secrets: DM.Tools.Seeder writes exactly these into
 * a throwaway local database and the same pairs live in the seeder source.
 * Overridable through the environment for a differently seeded stack.
 *
 * What was here before: a user named "Alice" with an empty password, plus ten
 * spec files each carrying their own hardcoded copy of a password for that
 * same non-existent user. Login therefore always failed; every authenticated
 * test skipped itself through `test.skip(!authContext, "Auth failed")` and the
 * suite reported green while 37 tests had not run in a long time. A second,
 * independent break hid underneath: loginWithCookies called `newContext()` on
 * the injected APIRequestContext, which has no such method.
 */
export const primaryUser = {
  username: process.env.E2E_TEST_USERNAME || "SolohinLex",
  email: process.env.E2E_TEST_EMAIL || "admin@test.local",
  password: process.env.E2E_TEST_PASSWORD || "Test123!",
};

/** A second seeded account, for tests that need two distinct users. */
export const secondaryUser = {
  username: process.env.E2E_SECOND_USERNAME || "TestUser",
  email: process.env.E2E_SECOND_EMAIL || "user@test.local",
  password: process.env.E2E_SECOND_PASSWORD || "Test123!",
};

/** Kept for call sites that read the primary account's name. */
export const seededUser = primaryUser;

// ESM: there is no __dirname, so the path comes from import.meta.url.
const AUTH_DIR = join(dirname(fileURLToPath(import.meta.url)), "..", ".auth");
export const PRIMARY_STORAGE_STATE = join(AUTH_DIR, "primary.json");
export const SECONDARY_STORAGE_STATE = join(AUTH_DIR, "secondary.json");

/**
 * A request context carrying a session saved by global setup. No login call,
 * so the 5-per-minute auth rate limit is never a factor no matter how many
 * workers run.
 */
export function authenticatedContext(
  storageState: string = PRIMARY_STORAGE_STATE,
): Promise<APIRequestContext> {
  return playwrightRequest.newContext({ storageState });
}

export const test = base.extend<{
  authenticatedPage: Page;
  /** API request context signed in as the primary seeded account. */
  authContext: APIRequestContext;
}>({
  authenticatedPage: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: PRIMARY_STORAGE_STATE,
    });
    const page = await context.newPage();
    await use(page);
    await context.close();
  },

  // eslint-disable-next-line no-empty-pattern -- the fixture depends on no other
  authContext: async ({}, use) => {
    const context = await authenticatedContext();
    await use(context);
    await context.dispose();
  },
});

export { expect };
