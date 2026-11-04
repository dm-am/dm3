import { test, expect, API_BASE_URL } from "../../fixtures/auth";

test.describe("Blacklist API", () => {
  test("should get my blacklist", async ({ authContext }) => {
    const response = await authContext.get(
      `${API_BASE_URL}/v1/users/me/blacklist`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test("should get blacklist settings", async ({ authContext }) => {
    const response = await authContext.get(
      `${API_BASE_URL}/v1/users/me/blacklist/settings`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("hideComments");
    expect(data).toHaveProperty("hideMessages");
    expect(data).toHaveProperty("blockDirectMessages");
    expect(data).toHaveProperty("autoPopulateContentBlacklist");
  });

  test("should update blacklist settings", async ({ authContext }) => {
    const response = await authContext.patch(
      `${API_BASE_URL}/v1/users/me/blacklist/settings`,
      {
        data: {
          hideComments: true,
          blockDirectMessages: false,
        },
      },
    );

    expect(response.ok()).toBeTruthy();
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(`${API_BASE_URL}/v1/users/me/blacklist`);
    expect(response.status()).toBe(401);
  });
});
