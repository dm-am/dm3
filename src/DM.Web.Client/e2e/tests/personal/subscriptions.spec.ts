import { test, expect, type APIRequestContext } from "@playwright/test";
import { API_BASE_URL, authenticatedContext } from "../../fixtures/auth";

/** SubscriptionTargetType.Game. */
const GAME_TARGET_TYPE = 1;

let authContext: APIRequestContext;
const createdSubscriptionIds: string[] = [];

test.beforeAll(async () => {
  authContext = await authenticatedContext();
});

test.afterAll(async () => {
  // Best-effort cleanup of everything this worker subscribed to; a second
  // delete of a subscription a test already removed is expected.
  for (const id of createdSubscriptionIds) {
    await authContext.delete(`${API_BASE_URL}/v1/users/me/subscriptions/${id}`);
  }

  if (authContext) await authContext.dispose();
});

/**
 * The id of a seeded game at the given position. Tests that create or remove
 * a subscription take different games: a subscription is unique per user and
 * target, and fullyParallel puts the tests of one file in different workers.
 */
async function gameId(index: number): Promise<string> {
  const response = await authContext.get(`${API_BASE_URL}/v1/games?take=3`);

  expect(response.ok()).toBeTruthy();
  const games = await response.json();
  expect(games).toHaveProperty("resources");
  expect(games.resources.length).toBeGreaterThan(index);
  return games.resources[index].id;
}

/** Subscribes the signed-in user to a game and returns the subscription id. */
async function subscribeToGame(targetId: string): Promise<string> {
  const response = await authContext.post(
    `${API_BASE_URL}/v1/users/me/subscriptions`,
    {
      data: {
        targetType: GAME_TARGET_TYPE,
        targetId,
      },
    },
  );

  // Subscribing twice is not an error: the endpoint answers with the existing
  // subscription, under the same 201.
  expect(response.status()).toBe(201);
  const subscription = await response.json();
  expect(subscription).toHaveProperty("id");
  createdSubscriptionIds.push(subscription.id);
  return subscription.id;
}

test.describe("Subscriptions API", () => {
  test("should get my subscriptions list", async () => {
    const response = await authContext.get(
      `${API_BASE_URL}/v1/users/me/subscriptions`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test("should check subscription status", async () => {
    const targetId = await gameId(0);

    // "Not subscribed" is a documented 404: the endpoint answers with the
    // subscription or with nothing, depending on whether this worker has
    // already subscribed to that game, so both are accepted.
    const response = await authContext.get(
      `${API_BASE_URL}/v1/users/me/subscriptions/check?type=Game&targetId=${targetId}`,
    );

    // Only the status, and no branch on it: the shape of the 200 body is what
    // the next test asserts, on a subscription it created itself and can
    // therefore count on.
    expect([200, 404]).toContain(response.status());
  });

  test("should subscribe to a game", async () => {
    const targetId = await gameId(1);
    const subscriptionId = await subscribeToGame(targetId);

    // The subscription this test just created is the one the check endpoint
    // reports, so here 200 is the only acceptable answer.
    const checkResponse = await authContext.get(
      `${API_BASE_URL}/v1/users/me/subscriptions/check?type=Game&targetId=${targetId}`,
    );

    expect(checkResponse.status()).toBe(200);
    const subscription = await checkResponse.json();
    expect(subscription.id).toBe(subscriptionId);
  });

  test("should unsubscribe", async () => {
    const subscriptionId = await subscribeToGame(await gameId(2));

    const response = await authContext.delete(
      `${API_BASE_URL}/v1/users/me/subscriptions/${subscriptionId}`,
    );

    expect(response.status()).toBe(204);
  });

  test("should require authentication", async ({ request }) => {
    const response = await request.get(
      `${API_BASE_URL}/v1/users/me/subscriptions`,
    );

    expect(response.status()).toBe(401);
  });
});
