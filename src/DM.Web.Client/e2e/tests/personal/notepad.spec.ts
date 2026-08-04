import { test, expect, type APIRequestContext } from "@playwright/test";
import { authenticatedContext } from "../../fixtures/auth";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

let authContext: APIRequestContext;
const createdEntryIds: string[] = [];

test.beforeAll(async () => {
  authContext = await authenticatedContext();
});

test.afterAll(async () => {
  // Best-effort cleanup of everything this worker created; a second delete of
  // an entry a test already removed is expected and its answer is ignored.
  for (const id of createdEntryIds) {
    await authContext.delete(`${API_URL}/v1/users/me/notepad/${id}`);
  }
  if (authContext) await authContext.dispose();
});

/**
 * Creates an entry and returns it, so every test owns the entry it works on.
 *
 * The read, update and delete tests used to share one entry created by the
 * test before them and opened with `test.skip(!createdEntryId, ...)`. With
 * fullyParallel the tests of one file can land in different workers, where a
 * module-level id set elsewhere simply does not exist — so those three
 * reported green exactly when they had nothing to work on. Both halves are
 * fixed here: the entry is per test, and a creation that fails fails the test.
 *
 * A single resource travels inside an envelope — see API_DESIGN.md.
 */
async function createEntry(title: string) {
  const response = await authContext.post(`${API_URL}/v1/users/me/notepad`, {
    headers: { "Content-Type": "application/json" },
    data: {
      title,
      content: "[b]Test content[/b] from E2E tests",
    },
  });

  expect(response.status()).toBe(201);
  const { resource } = await response.json();
  expect(resource).toHaveProperty("id");
  createdEntryIds.push(resource.id);
  return resource;
}

test.describe("Notepad API", () => {
  test("should get my notepad entries", async () => {
    const response = await authContext.get(`${API_URL}/v1/users/me/notepad`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test("should create notepad entry", async () => {
    const entry = await createEntry("E2E Test Entry");

    expect(entry).toHaveProperty("title", "E2E Test Entry");
  });

  test("should get notepad entry by id", async () => {
    const entry = await createEntry("E2E Read Entry");

    const response = await authContext.get(
      `${API_URL}/v1/users/me/notepad/${entry.id}`,
    );

    expect(response.ok()).toBeTruthy();
    const { resource } = await response.json();
    expect(resource).toHaveProperty("id", entry.id);
    expect(resource).toHaveProperty("title");
    expect(resource).toHaveProperty("content");
  });

  test("should update notepad entry", async () => {
    const entry = await createEntry("E2E Update Entry");

    const response = await authContext.patch(
      `${API_URL}/v1/users/me/notepad/${entry.id}`,
      {
        headers: { "Content-Type": "application/json" },
        data: {
          title: "Updated E2E Entry",
        },
      },
    );

    expect(response.ok()).toBeTruthy();
    const { resource } = await response.json();
    expect(resource).toHaveProperty("title", "Updated E2E Entry");
  });

  test("should delete notepad entry", async () => {
    const entry = await createEntry("E2E Delete Entry");

    const response = await authContext.delete(
      `${API_URL}/v1/users/me/notepad/${entry.id}`,
    );

    expect(response.status()).toBe(204);
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/users/me/notepad`);
    expect(response.status()).toBe(401);
  });
});
