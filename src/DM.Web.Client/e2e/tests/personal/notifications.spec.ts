import { test, expect, type APIRequestContext } from "@playwright/test";
import { authenticatedContext } from "../../fixtures/auth";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

let authContext: APIRequestContext;

test.beforeAll(async () => {
  authContext = await authenticatedContext();
});

test.afterAll(async () => {
  if (authContext) await authContext.dispose();
});

test.describe("Notifications API", () => {
  test("should get notifications list", async () => {
    const response = await authContext.get(
      `${API_URL}/v1/users/me/notifications`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
    // A row says what happened, where it leads and when, in the server's words:
    // the page names no event and builds no path of its own. The path is left
    // out of a row that leads nowhere (the API writes no null fields). The seed
    // writes no notifications, so on a fresh stand the list may hold none, and
    // the rows the account has gathered are what is read.
    type Detail = { label: unknown; value: unknown; isMoment: unknown };
    const told = (row: Record<string, unknown>) =>
      typeof row.title === "string" &&
      row.title.length > 0 &&
      typeof row.isRead === "boolean" &&
      typeof row.createdUtc === "string" &&
      !Number.isNaN(Date.parse(row.createdUtc)) &&
      (!("url" in row) ||
        (typeof row.url === "string" && row.url.startsWith("/"))) &&
      Array.isArray(row.details) &&
      (row.details as Detail[]).every(
        (detail) =>
          typeof detail.label === "string" &&
          typeof detail.value === "string" &&
          typeof detail.isMoment === "boolean",
      );
    const untold = (data.resources as Record<string, unknown>[]).filter(
      (row) => !told(row),
    );
    expect(untold).toEqual([]);
  });

  test("should get unread count", async () => {
    const response = await authContext.get(
      `${API_URL}/v1/users/me/notifications/unread`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("count");
    expect(typeof data.count).toBe("number");
  });

  test("should mark all as read", async () => {
    const response = await authContext.delete(
      `${API_URL}/v1/users/me/notifications/unread`,
    );

    expect(response.status()).toBe(204);
  });

  test("should get notification settings", async () => {
    const response = await authContext.get(
      `${API_URL}/v1/users/me/notifications/settings`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    // Settings object has discord/telegram sub-objects
    expect(typeof data).toBe("object");
  });

  test("should update notification settings", async () => {
    const response = await authContext.patch(
      `${API_URL}/v1/users/me/notifications/settings`,
      {
        headers: { "Content-Type": "application/json" },
        data: {
          discord: {
            enabled: false,
          },
        },
      },
    );

    expect(response.ok()).toBeTruthy();
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/users/me/notifications`);
    expect(response.status()).toBe(401);
  });
});
