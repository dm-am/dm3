import { test, expect, type APIRequestContext } from "@playwright/test";
import {
  API_BASE_URL,
  authenticatedContext,
  primaryUser,
} from "../../fixtures/auth";

/**
 * Personal notes about other users: `/v1/users/me/notes/{username}`, an upsert
 * keyed by the subject. There is no collection: no POST to create, no GET to
 * list.
 */

let authContext: APIRequestContext;
let subjectUsername: string | null = null;

test.beforeAll(async () => {
  authContext = await authenticatedContext();

  const usersResponse = await authContext.get(
    `${API_BASE_URL}/v1/users?take=5`,
  );
  const users = await usersResponse.json();
  subjectUsername =
    users.resources?.find(
      (u: { username: string }) => u.username !== primaryUser.username,
    )?.username ?? null;
});

test.afterAll(async () => {
  if (subjectUsername) {
    await authContext.delete(
      `${API_BASE_URL}/v1/users/me/notes/${subjectUsername}`,
    );
  }
  await authContext.dispose();
});

test.describe("Profile Notes API", () => {
  // One note, one pair of users: run in parallel, the test that clears it
  // took the note from under the one that reads it, which then saw no note.
  test.describe.configure({ mode: "serial" });

  test("upserts a note about another user", async () => {
    expect(subjectUsername, "seed must contain a second user").not.toBeNull();

    const response = await authContext.put(
      `${API_BASE_URL}/v1/users/me/notes/${subjectUsername}`,
      {
        data: { text: "Заметка из e2e" },
      },
    );

    expect(response.status()).toBe(200);
    const data = await response.json();
    expect(data).toHaveProperty("text", "Заметка из e2e");
  });

  test("reads the note back by username", async () => {
    // Its own precondition, whatever the test above left.
    await authContext.put(
      `${API_BASE_URL}/v1/users/me/notes/${subjectUsername}`,
      {
        data: { text: "Заметка из e2e" },
      },
    );
    const response = await authContext.get(
      `${API_BASE_URL}/v1/users/me/notes/${subjectUsername}`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("text");
  });

  test("deletes a note by writing an empty text", async () => {
    const response = await authContext.put(
      `${API_BASE_URL}/v1/users/me/notes/${subjectUsername}`,
      {
        data: { text: "" },
      },
    );

    expect(response.status()).toBe(204);
  });

  test("refuses a note about oneself", async () => {
    const response = await authContext.put(
      `${API_BASE_URL}/v1/users/me/notes/${primaryUser.username}`,
      {
        data: { text: "Заметка о себе" },
      },
    );

    expect(response.status()).toBe(400);
  });

  test("reports an unknown subject as not found", async () => {
    const response = await authContext.put(
      `${API_BASE_URL}/v1/users/me/notes/nobody-by-that-name`,
      {
        data: { text: "Заметка о призраке" },
      },
    );

    expect(response.status()).toBe(404);
  });

  test("requires authentication", async ({ request }) => {
    const response = await request.get(
      `${API_BASE_URL}/v1/users/me/notes/${primaryUser.username}`,
    );

    expect(response.status()).toBe(401);
  });
});
