import type { Browser, BrowserContext, Page } from "@playwright/test";

/** A window of a width: a context of its own and not the page of the project. */

/** A window as wide as given, with the session given or none. */
export interface WindowScreen {
  width: number;
  storageState?: string;
}

/** A context of a window, 900 high. The caller closes it. */
export function windowContext(
  browser: Browser,
  { width, storageState }: WindowScreen,
): Promise<BrowserContext> {
  return browser.newContext({ storageState, viewport: { width, height: 900 } });
}

/** A page of a window, as the session given or as a guest; it closes with its context. */
export async function windowPage(
  browser: Browser,
  width: number,
  storageState?: string,
): Promise<Page> {
  const context = await windowContext(browser, { width, storageState });
  return context.newPage();
}

/** Two frames after the face has loaded: a table settles after its column. */
export async function settle(page: Page) {
  await page.evaluate(() =>
    document.fonts.ready.then(
      () =>
        new Promise<void>((done) =>
          requestAnimationFrame(() => requestAnimationFrame(() => done())),
        ),
    ),
  );
}
