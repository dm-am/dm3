import { test, expect } from "@playwright/test";
import { primaryUser } from "../../fixtures/auth";

/**
 * Three assertions, none of which could hold. The profile lives at
 * `/users/:username`, not at the site root, so `/Alice` was a 404 page and
 * "Alice" is not an account the seeder writes. ".profile-info",
 * ".user-information", ".user-games" and ".user-characters" are in no
 * template. And the tabs are pure client state — `/users/X/games` redirects
 * to the profile with the default tab, and there is no characters tab at all.
 */
const profile = `/users/${primaryUser.username}`;

test.describe("User Profiles", () => {
  test("should display user profile", async ({ page }) => {
    await page.goto(profile);

    await expect(page.locator(".profile-page")).toBeVisible();
    await expect(
      page.getByRole("heading", { name: `Профиль: ${primaryUser.username}` }),
    ).toBeVisible();
  });

  test("should open the games tab", async ({ page }) => {
    await page.goto(profile);

    // The seeded account hosts games, so the tab is not hidden.
    await page.getByRole("tab", { name: "Игры" }).click();
    await expect(page.locator(".profile-games-table")).toBeVisible();
  });

  test("should keep the tab out of the URL", async ({ page }) => {
    // A legacy /users/X/games link lands on the profile itself: the tab is
    // client state and never appears in the address.
    await page.goto(`${profile}/games`);

    await expect(page).toHaveURL(new RegExp(`${profile}$`));
    await expect(page.locator(".profile-page")).toBeVisible();
  });
});
