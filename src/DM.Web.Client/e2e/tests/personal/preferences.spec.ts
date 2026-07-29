import { test, expect, APIRequestContext } from "@playwright/test";
import { authenticatedContext } from "../../fixtures/auth";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

let authContext: APIRequestContext;

test.beforeAll(async () => {
  authContext = await authenticatedContext();
});

test.afterAll(async () => {
  if (authContext) await authContext.dispose();
});

test.describe("Preferences API", () => {
  test("should get my preferences", async () => {
    const response = await authContext.get(
      `${API_URL}/v1/users/me/preferences`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("paging");
  });

  test("should update paging preferences", async () => {
    const response = await authContext.patch(
      `${API_URL}/v1/users/me/preferences`,
      {
        headers: { "Content-Type": "application/json" },
        data: {
          paging: {
            postsPerPage: 25,
            commentsPerPage: 20,
            topicsPerPage: 30,
            messagesPerPage: 20,
            entitiesPerPage: 20,
          },
        },
      },
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.paging).toHaveProperty("postsPerPage", 25);
    expect(data.paging).toHaveProperty("entitiesPerPage", 20);
  });

  test("should update entitiesPerPage preference", async () => {
    const response = await authContext.patch(
      `${API_URL}/v1/users/me/preferences`,
      {
        headers: { "Content-Type": "application/json" },
        data: {
          paging: {
            entitiesPerPage: 15,
          },
        },
      },
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.paging).toHaveProperty("entitiesPerPage", 15);

    // Reset to default
    await authContext.patch(`${API_URL}/v1/users/me/preferences`, {
      headers: { "Content-Type": "application/json" },
      data: {
        paging: {
          entitiesPerPage: 20,
        },
      },
    });
  });

  test("should return all paging fields", async () => {
    const response = await authContext.get(
      `${API_URL}/v1/users/me/preferences`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.paging).toHaveProperty("postsPerPage");
    expect(data.paging).toHaveProperty("commentsPerPage");
    expect(data.paging).toHaveProperty("topicsPerPage");
    expect(data.paging).toHaveProperty("messagesPerPage");
    expect(data.paging).toHaveProperty("entitiesPerPage");
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/users/me/preferences`);
    expect(response.status()).toBe(401);
  });
});
