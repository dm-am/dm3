import { test, expect, type APIRequestContext } from "@playwright/test";
import { authenticatedContext, seededUser } from "../../fixtures/auth";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

/**
 * Personal notes about other users: `/v1/users/me/notes/{username}`, an upsert
 * keyed by the subject. There is no collection: no POST to create, no GET to
 * list. The previous version of this file tested exactly those two, got 404
 * from a route that has never existed, and did not notice because the whole
 * suite was skipping itself.
 */

let authContext: APIRequestContext;
let subjectUsername: string | null = null;

test.beforeAll(async () => {
  authContext = await authenticatedContext();

  const usersResponse = await authContext.get(`${API_URL}/v1/users?size=5`);
  const users = await usersResponse.json();
  subjectUsername =
    users.resources?.find(
      (u: { username: string }) => u.username !== seededUser.username,
    )?.username ?? null;
});

test.afterAll(async () => {
  if (subjectUsername) {
    await authContext.delete(`${API_URL}/v1/users/me/notes/${subjectUsername}`);
  }
  await authContext.dispose();
});

test.describe("Profile Notes API", () => {
  test("upserts a note about another user", async () => {
    expect(subjectUsername, "seed must contain a second user").not.toBeNull();

    const response = await authContext.put(
      `${API_URL}/v1/users/me/notes/${subjectUsername}`,
      {
        headers: { "Content-Type": "application/json" },
        data: { text: "Заметка из e2e" },
      },
    );

    expect(response.status()).toBe(200);
    const data = await response.json();
    expect(data).toHaveProperty("text", "Заметка из e2e");
  });

  test("reads the note back by username", async () => {
    const response = await authContext.get(
      `${API_URL}/v1/users/me/notes/${subjectUsername}`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("text");
  });

  test("deletes a note by writing an empty text", async () => {
    const response = await authContext.put(
      `${API_URL}/v1/users/me/notes/${subjectUsername}`,
      {
        headers: { "Content-Type": "application/json" },
        data: { text: "" },
      },
    );

    expect(response.status()).toBe(204);
  });

  test("refuses a note about oneself", async () => {
    const response = await authContext.put(
      `${API_URL}/v1/users/me/notes/${seededUser.username}`,
      {
        headers: { "Content-Type": "application/json" },
        data: { text: "Заметка о себе" },
      },
    );

    expect(response.status()).toBe(400);
  });

  test("reports an unknown subject as not found", async () => {
    const response = await authContext.put(
      `${API_URL}/v1/users/me/notes/nobody-by-that-name`,
      {
        headers: { "Content-Type": "application/json" },
        data: { text: "Заметка о призраке" },
      },
    );

    expect(response.status()).toBe(404);
  });

  test("requires authentication", async ({ request }) => {
    const response = await request.get(
      `${API_URL}/v1/users/me/notes/${seededUser.username}`,
    );

    expect(response.status()).toBe(401);
  });
});
