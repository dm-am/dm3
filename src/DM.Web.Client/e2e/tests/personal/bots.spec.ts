import { test, expect, API_BASE_URL } from "../../fixtures/auth";

test.describe("Notification Bots API", () => {
  test("should get telegram link code", async ({ authContext }) => {
    const response = await authContext.post(
      `${API_BASE_URL}/v1/users/me/notifications/bots/telegram`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("code");
    expect(data).toHaveProperty("expiresUtc");
    expect(data.code).toHaveLength(6);
  });

  test("should get discord link code", async ({ authContext }) => {
    const response = await authContext.post(
      `${API_BASE_URL}/v1/users/me/notifications/bots/discord`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("code");
    expect(data.code).toHaveLength(6);
  });

  test("should reject invalid bot type", async ({ authContext }) => {
    const response = await authContext.post(
      `${API_BASE_URL}/v1/users/me/notifications/bots/invalid`,
    );

    expect(response.status()).toBe(400);
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.post(
      `${API_BASE_URL}/v1/users/me/notifications/bots/telegram`,
    );
    expect(response.status()).toBe(401);
  });
});
