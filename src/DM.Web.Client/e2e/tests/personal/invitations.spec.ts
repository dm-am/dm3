import { test, expect, API_BASE_URL } from "../../fixtures/auth";

test.describe("Invitations API", () => {
  test("should get my invitations", async ({ authContext }) => {
    const response = await authContext.get(
      `${API_BASE_URL}/v1/users/me/invitations`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test("should reject non-existent invitation", async ({ authContext }) => {
    const fakeId = "00000000-0000-0000-0000-000000000000";
    const response = await authContext.post(
      `${API_BASE_URL}/v1/users/me/invitations/${fakeId}/reject`,
    );

    expect(response.status()).toBe(404);
  });

  test("should accept non-existent invitation", async ({ authContext }) => {
    const fakeId = "00000000-0000-0000-0000-000000000000";
    const response = await authContext.post(
      `${API_BASE_URL}/v1/users/me/invitations/${fakeId}/accept`,
    );

    expect(response.status()).toBe(404);
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(
      `${API_BASE_URL}/v1/users/me/invitations`,
    );
    expect(response.status()).toBe(401);
  });
});
