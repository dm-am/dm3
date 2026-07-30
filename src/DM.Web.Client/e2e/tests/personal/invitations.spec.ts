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

test.describe("Invitations API", () => {
  test("should get my invitations", async () => {
    const response = await authContext.get(
      `${API_URL}/v1/users/me/invitations`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test("should reject non-existent invitation", async () => {
    const fakeId = "00000000-0000-0000-0000-000000000000";
    const response = await authContext.post(
      `${API_URL}/v1/users/me/invitations/${fakeId}/reject`,
    );

    expect(response.status()).toBe(404);
  });

  test("should accept non-existent invitation", async () => {
    const fakeId = "00000000-0000-0000-0000-000000000000";
    const response = await authContext.post(
      `${API_URL}/v1/users/me/invitations/${fakeId}/accept`,
    );

    expect(response.status()).toBe(404);
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/users/me/invitations`);
    expect(response.status()).toBe(401);
  });
});
