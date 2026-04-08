import { test, expect, APIRequestContext } from "@playwright/test";
import { loginWithCookies } from "../../fixtures/auth";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

const TEST_USER = {
  username: "Alice",
  password: "Xk9#mQz2$vL7nW",
};

let authContext: APIRequestContext;

test.beforeAll(async ({ request }) => {
  try {
    authContext = await loginWithCookies(
      request,
      TEST_USER.username,
      TEST_USER.password,
    );
  } catch (e) {
    console.error("Failed to login:", e);
  }
});

test.afterAll(async () => {
  if (authContext) await authContext.dispose();
});

test.describe("Personal Profile API", () => {
  test("should get my profile", async () => {
    test.skip(!authContext, "Auth failed");

    const response = await authContext.get(`${API_URL}/v1/users/me/profile`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("username", TEST_USER.username);
    expect(data).toHaveProperty("email");
    expect(data).toHaveProperty("visibility");
  });

  test("should update my profile status", async () => {
    test.skip(!authContext, "Auth failed");

    const newStatus = `Test status ${Date.now()}`;

    const response = await authContext.patch(`${API_URL}/v1/users/me/profile`, {
      headers: { "Content-Type": "application/json" },
      data: { status: newStatus },
    });

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("status", newStatus);
  });

  test("should update visibility settings", async () => {
    test.skip(!authContext, "Auth failed");

    const response = await authContext.patch(`${API_URL}/v1/users/me/profile`, {
      headers: { "Content-Type": "application/json" },
      data: {
        visibility: {
          showBirthday: true,
          showRating: true,
        },
      },
    });

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.visibility).toHaveProperty("showBirthday", true);
    expect(data.visibility).toHaveProperty("showRating", true);
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/users/me/profile`);
    expect(response.status()).toBe(401);
  });
});
