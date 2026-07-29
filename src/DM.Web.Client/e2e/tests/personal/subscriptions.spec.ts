import { test, expect, APIRequestContext } from "@playwright/test";
import { authenticatedContext } from "../../fixtures/auth";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

let authContext: APIRequestContext;
let testSubscriptionId: string | null = null;

test.beforeAll(async () => {
  authContext = await authenticatedContext();
});

test.afterAll(async () => {
  // Cleanup: unsubscribe if we created one
  if (testSubscriptionId && authContext) {
    await authContext.delete(
      `${API_URL}/v1/users/me/subscriptions/${testSubscriptionId}`,
    );
  }

  if (authContext) await authContext.dispose();
});

test.describe("Subscriptions API", () => {
  test("should get my subscriptions list", async () => {
    const response = await authContext.get(
      `${API_URL}/v1/users/me/subscriptions`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test("should check subscription status", async () => {
    // Check subscription to a game (need a valid game ID)
    // First, get a game to subscribe to
    const gamesResponse = await authContext.get(`${API_URL}/v1/games?size=1`);
    if (!gamesResponse.ok()) {
      test.skip(true, "Cannot get games");
      return;
    }

    const games = await gamesResponse.json();
    if (!games.resources || games.resources.length === 0) {
      test.skip(true, "No games available");
      return;
    }

    const gameId = games.resources[0].id;

    // Check subscription status
    const response = await authContext.get(
      `${API_URL}/v1/users/me/subscriptions/check?targetType=1&targetId=${gameId}`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    // Response is subscription object or null indicator
    expect(typeof data).toBe("object");
  });

  test("should subscribe to a game", async () => {
    // Get a game to subscribe to
    const gamesResponse = await authContext.get(`${API_URL}/v1/games?size=1`);
    if (!gamesResponse.ok()) {
      test.skip(true, "Cannot get games");
      return;
    }

    const games = await gamesResponse.json();
    if (!games.resources || games.resources.length === 0) {
      test.skip(true, "No games available");
      return;
    }

    const gameId = games.resources[0].id;

    // Subscribe
    const response = await authContext.post(
      `${API_URL}/v1/users/me/subscriptions`,
      {
        headers: { "Content-Type": "application/json" },
        data: {
          targetType: 1, // Game
          targetId: gameId,
        },
      },
    );

    // 201 Created or 409 if already subscribed
    expect([201, 409]).toContain(response.status());

    if (response.status() === 201) {
      const data = await response.json();
      testSubscriptionId = data?.id;
    }
  });

  test("should unsubscribe", async () => {
    test.skip(!testSubscriptionId, "No subscription to delete");

    const response = await authContext.delete(
      `${API_URL}/v1/users/me/subscriptions/${testSubscriptionId}`,
    );

    expect(response.status()).toBe(204);
    testSubscriptionId = null;
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/users/me/subscriptions`);

    expect(response.status()).toBe(401);
  });
});
