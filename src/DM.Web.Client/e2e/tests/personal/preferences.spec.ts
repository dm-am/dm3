import { test, expect, API_BASE_URL } from "../../fixtures/auth";

/**
 * The account's own settings: one page size for every paged list, the time
 * zone and the theme a new device starts from. The API reads and writes the
 * three, the section "Личные настройки" of /account shows them as saved. The
 * part in the rating is the profile's (users/me/profile, visibility). Every
 * write is put back as the account had it.
 */

const PREFERENCES = `${API_BASE_URL}/v1/users/me/preferences`;

test.describe("Preferences API", () => {
  test("should get my preferences", async ({ authContext }) => {
    const response = await authContext.get(PREFERENCES);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    // The API leaves a null out: an account with no zone (UTC) comes without
    // timeZoneId, so the zone is checked only when it is there.
    const keys = Object.keys(data).filter((key) => key !== "timeZoneId");
    expect(keys.sort()).toEqual(["pageSize", "theme"]);
    if ("timeZoneId" in data) expect(typeof data.timeZoneId).toBe("string");
  });

  test("should update the page size alone", async ({ authContext }) => {
    const before = await (await authContext.get(PREFERENCES)).json();

    const response = await authContext.patch(PREFERENCES, {
      data: { pageSize: 50 },
    });

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.pageSize).toBe(50);
    // A field left out of the request keeps what the account has.
    expect(data.theme).toBe(before.theme);

    await authContext.patch(PREFERENCES, {
      data: { pageSize: before.pageSize },
    });
  });

  test("should refuse a page size the lists do not offer", async ({
    authContext,
  }) => {
    const response = await authContext.patch(PREFERENCES, {
      data: { pageSize: 7 },
    });

    expect(response.status()).toBe(400);
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(PREFERENCES);
    expect(response.status()).toBe(401);
  });
});

test.describe("the section of the account's own settings", () => {
  // A page of settings makes dozens of reads.
  test.describe.configure({ timeout: 120_000 });

  test("shows one page size and the theme of a new device, as saved, and no part in the rating", async ({
    authenticatedPage: page,
    authContext,
  }) => {
    const saved = await (await authContext.get(PREFERENCES)).json();

    await page.goto("/account");
    const section = page.locator("section.section").filter({
      has: page.getByRole("heading", { name: "Личные настройки" }),
    });
    const pageSize = section.locator("#account-page-size");
    await expect(pageSize).toBeVisible();

    // One list for every paged list, not one per kind.
    await expect(
      section.getByText("Сколько показывать на странице"),
    ).toBeVisible();
    await expect(section.locator("select")).toHaveCount(3);
    await expect(pageSize).toHaveValue(String(saved.pageSize));

    await expect(section.getByText("Тема на новом устройстве")).toBeVisible();
    await expect(section.locator("#account-theme")).toHaveValue(saved.theme);

    // The part in the rating is set in the edit mode of the profile.
    await expect(section.getByText("Участие в рейтинге")).toHaveCount(0);
  });
});
