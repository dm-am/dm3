import { test, expect, API_BASE_URL, primaryUser } from "../../fixtures/auth";

test.describe("Personal Profile API", () => {
  test("should get my profile", async ({ authContext }) => {
    const response = await authContext.get(
      `${API_BASE_URL}/v1/users/me/profile`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("username", primaryUser.username);
    expect(data).toHaveProperty("email");
    expect(data).toHaveProperty("visibility");
  });

  test("should update my profile status", async ({ authContext }) => {
    const newStatus = `Test status ${Date.now()}`;

    const response = await authContext.patch(
      `${API_BASE_URL}/v1/users/me/profile`,
      {
        data: { status: newStatus },
      },
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("status", newStatus);
  });

  test("should update visibility settings", async ({ authContext }) => {
    const response = await authContext.patch(
      `${API_BASE_URL}/v1/users/me/profile`,
      {
        data: {
          visibility: {
            showBirthday: true,
            showRating: true,
          },
        },
      },
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.visibility).toHaveProperty("showBirthday", true);
    expect(data.visibility).toHaveProperty("showRating", true);
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(`${API_BASE_URL}/v1/users/me/profile`);
    expect(response.status()).toBe(401);
  });
});
