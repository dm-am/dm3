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

test.describe("Mirrors API", () => {
  test("should get list of mirrors", async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/mirrors`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("currentMirrorId");
    expect(data).toHaveProperty("mirrors");
    expect(Array.isArray(data.mirrors)).toBeTruthy();
  });

  test("should get transfer URL for authenticated user", async () => {
    test.skip(!authContext, "Auth failed");

    // Get transfer URL - we need a valid mirror ID from config
    // First get mirrors list
    const mirrorsResponse = await authContext.get(`${API_URL}/v1/mirrors`);
    expect(mirrorsResponse.ok()).toBeTruthy();

    const mirrors = await mirrorsResponse.json();
    if (mirrors.mirrors.length === 0) {
      test.skip(true, "No mirrors configured");
      return;
    }

    // Try to get transfer URL for first mirror
    const targetMirror = mirrors.mirrors[0].id;
    const transferResponse = await authContext.get(
      `${API_URL}/v1/mirrors/transfer?targetMirror=${targetMirror}`,
    );

    expect(transferResponse.ok()).toBeTruthy();
    const data = await transferResponse.json();
    expect(data).toHaveProperty("transferUrl");
  });

  test("should reject invalid mirror in transfer request", async () => {
    test.skip(!authContext, "Auth failed");

    const response = await authContext.get(
      `${API_URL}/v1/mirrors/transfer?targetMirror=nonexistent-mirror`,
    );

    expect(response.status()).toBe(400);
  });

  test("should reject invalid transfer token", async ({ request }) => {
    const response = await request.post(
      `${API_URL}/v1/mirrors/transfer/accept?transferToken=invalid-token`,
    );

    expect(response.status()).toBe(400);
  });
});
