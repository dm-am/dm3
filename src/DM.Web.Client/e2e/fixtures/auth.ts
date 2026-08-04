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

// ESM: __dirname отсутствует, путь берется из import.meta.url.
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

  // eslint-disable-next-line no-empty-pattern -- фикстура не зависит ни от одной другой
  authContext: async ({}, use) => {
    const context = await authenticatedContext();
    await use(context);
    await context.dispose();
  },
});

export { expect };
