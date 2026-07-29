import { test, expect, APIRequestContext } from "@playwright/test";
import { authenticatedContext, seededUser } from "../../fixtures/auth";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

let authContext: APIRequestContext;
let createdNoteId: string | null = null;

test.beforeAll(async () => {
  authContext = await authenticatedContext();
});

test.afterAll(async () => {
  // Cleanup
  if (createdNoteId && authContext) {
    await authContext.delete(`${API_URL}/v1/users/me/notes/${createdNoteId}`);
  }
  if (authContext) await authContext.dispose();
});

test.describe("Profile Notes API", () => {
  test("should create a note about another user", async () => {
    // Find another user to create a note about
    const usersResponse = await authContext.get(`${API_URL}/v1/users?size=5`);
    if (!usersResponse.ok()) {
      test.skip(true, "Cannot get users");
      return;
    }

    const users = await usersResponse.json();
    const otherUser = users.resources?.find(
      (u: { username: string }) => u.username !== seededUser.username,
    );
    if (!otherUser) {
      test.skip(true, "No other users available");
      return;
    }

    const response = await authContext.post(`${API_URL}/v1/users/me/notes`, {
      headers: { "Content-Type": "application/json" },
      data: {
        username: otherUser.username,
        text: "Test note from E2E",
      },
    });

    expect([200, 201]).toContain(response.status());
    const data = await response.json();
    if (data?.id) {
      createdNoteId = data.id;
    }
  });

  test("should get my notes", async () => {
    const response = await authContext.get(`${API_URL}/v1/users/me/notes`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test("should get note by username", async () => {
    test.skip(!createdNoteId, "No note to get");

    // Get the note we created
    const notesResponse = await authContext.get(`${API_URL}/v1/users/me/notes`);
    const notes = await notesResponse.json();

    if (notes.resources && notes.resources.length > 0) {
      const note = notes.resources[0];
      const response = await authContext.get(
        `${API_URL}/v1/users/me/notes/${note.username}`,
      );

      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data).toHaveProperty("text");
    }
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/users/me/notes`);
    expect(response.status()).toBe(401);
  });
});
