import {
  test as base,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
} from "./auth";
import { windowContext } from "./window";
import type { APIRequestContext, BrowserContext, Page } from "@playwright/test";

/**
 * The roles a page is read as, each by the session of a seeded account. The
 * administrator of the seed stands in for a moderator: the actions he sees are
 * a moderator's and more.
 */
export type Role = "guest" | "user" | "admin";

/** The session each role opens with; a guest has none. */
const SESSION: Record<Role, string | undefined> = {
  guest: undefined,
  user: SECONDARY_STORAGE_STATE,
  admin: PRIMARY_STORAGE_STATE,
};

/** A page to read, and the roles it is read as. */
export interface Target {
  /** The name of the test, where the address is found in the seed. */
  name?: string;
  path: string | ((api: APIRequestContext) => Promise<string>);
  roles: Role[];
  /** What has to be on the page before it is read. */
  ready: string;
  /** A step after the address opened, to reach the state read. */
  reach?: (page: Page) => Promise<void>;
}

export const test = base.extend<{
  /** Opens a page as a role at a width; the contexts close after the test. */
  openAs: (role: Role, width: number, target: Target) => Promise<Page>;
}>({
  openAs: async ({ browser, playwright }, use) => {
    const contexts: BrowserContext[] = [];
    const requests: APIRequestContext[] = [];
    await use(async (role, width, target) => {
      const context = await windowContext(browser, {
        width,
        storageState: SESSION[role],
      });
      contexts.push(context);
      let path = target.path;
      if (typeof path !== "string") {
        const api = await playwright.request.newContext({
          storageState: SESSION[role],
        });
        requests.push(api);
        path = await path(api);
      }
      const page = await context.newPage();
      await page.goto(path);
      await target.reach?.(page);
      return page;
    });
    for (const api of requests) await api.dispose();
    for (const context of contexts) await context.close();
  },
});
