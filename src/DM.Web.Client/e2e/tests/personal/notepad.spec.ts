import { test, expect, APIRequestContext } from "@playwright/test";
import { authenticatedContext } from "../../fixtures/auth";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

let authContext: APIRequestContext;
let createdEntryId: string | null = null;

test.beforeAll(async () => {
  authContext = await authenticatedContext();
});

test.afterAll(async () => {
  // Cleanup
  if (createdEntryId && authContext) {
    await authContext.delete(
      `${API_URL}/v1/users/me/notepad/${createdEntryId}`,
    );
  }
  if (authContext) await authContext.dispose();
});

test.describe("Notepad API", () => {
  test("should get my notepad entries", async () => {
    const response = await authContext.get(`${API_URL}/v1/users/me/notepad`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test("should create notepad entry", async () => {
    const response = await authContext.post(`${API_URL}/v1/users/me/notepad`, {
      headers: { "Content-Type": "application/json" },
      data: {
        title: "E2E Test Entry",
        content: "[b]Test content[/b] from E2E tests",
      },
    });

    expect(response.status()).toBe(201);
    const data = await response.json();
    expect(data).toHaveProperty("id");
    expect(data).toHaveProperty("title", "E2E Test Entry");
    createdEntryId = data.id;
  });

  test("should get notepad entry by id", async () => {
    test.skip(!createdEntryId, "No entry to get");

    const response = await authContext.get(
      `${API_URL}/v1/users/me/notepad/${createdEntryId}`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("id", createdEntryId);
    expect(data).toHaveProperty("title");
    expect(data).toHaveProperty("content");
  });

  test("should update notepad entry", async () => {
    test.skip(!createdEntryId, "No entry to update");

    const response = await authContext.patch(
      `${API_URL}/v1/users/me/notepad/${createdEntryId}`,
      {
        headers: { "Content-Type": "application/json" },
        data: {
          title: "Updated E2E Entry",
        },
      },
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("title", "Updated E2E Entry");
  });

  test("should delete notepad entry", async () => {
    test.skip(!createdEntryId, "No entry to delete");

    const response = await authContext.delete(
      `${API_URL}/v1/users/me/notepad/${createdEntryId}`,
    );

    expect(response.status()).toBe(204);
    createdEntryId = null;
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/users/me/notepad`);
    expect(response.status()).toBe(401);
  });
});
