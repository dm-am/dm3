import { test, expect } from "../../fixtures/auth";
import type { Page } from "@playwright/test";

/**
 * A tab left open across a deploy asks for chunks the new release no longer
 * has (staleBundle.ts). Each browser words that failure its own way, so the
 * net is read in each of them, on a build: Vite's preload helper, which fires
 * the event the net listens to, exists only there. A chunk is made to vanish by
 * failing its request, the way a renamed file answers.
 *
 * Read as a guest: the pages taken here need no session.
 */

/** A line only the page "О проекте" draws. */
const ABOUT_MOTTO = "Мы в ответе за тех, кого сгенерили!";

/** Counts the documents the tab loads, the first one included. */
function countDocumentLoads(page: Page): () => number {
  let loads = 0;
  page.on("request", (request) => {
    if (request.isNavigationRequest() && request.frame() === page.mainFrame()) {
      loads += 1;
    }
  });
  return () => loads;
}

/**
 * Long enough for a reload loop to show itself: a loop reloads as soon as the
 * page asks for the chunk again, well inside this.
 */
const LOOP_WINDOW_MS = 3_000;

async function openHome(page: Page): Promise<void> {
  await page.goto("/");
  await page.waitForLoadState("load");
}

/**
 * Clicks the menu link without moving the pointer: a pointer on the link
 * prefetches its chunk, which would spend the one failure meant for the click.
 */
async function clickAbout(page: Page): Promise<void> {
  await page
    .getByRole("link", { name: "О проекте", exact: true })
    .first()
    .dispatchEvent("click");
}

test.describe("a chunk of the old deploy that is gone", () => {
  test("reloads once into the page whose script is gone", async ({ page }) => {
    const loads = countDocumentLoads(page);
    await openHome(page);
    await page.route("**/assets/AboutPage-*.js", (route) => route.abort(), {
      times: 1,
    });

    await clickAbout(page);

    await expect(page).toHaveURL(/\/about$/);
    await expect(page.getByText(ABOUT_MOTTO)).toBeVisible();
    expect(loads()).toBe(2);
  });

  test("reloads once into the page whose stylesheet is gone", async ({
    page,
  }) => {
    const loads = countDocumentLoads(page);
    await openHome(page);
    await page.route("**/assets/AboutPage-*.css", (route) => route.abort(), {
      times: 1,
    });

    await clickAbout(page);

    await expect(page).toHaveURL(/\/about$/);
    await expect(page.getByText(ABOUT_MOTTO)).toBeVisible();
    expect(loads()).toBe(2);
  });

  test("reloads once, and only once, when a chunk outside the router is gone for good", async ({
    page,
  }) => {
    const loads = countDocumentLoads(page);
    // The realtime transport, which every page loads as it mounts.
    await page.route("**/assets/signalr-*.js", (route) => route.abort());

    await openHome(page);

    await expect.poll(loads).toBe(2);
    await page.waitForTimeout(LOOP_WINDOW_MS);
    expect(loads()).toBe(2);
  });
});
