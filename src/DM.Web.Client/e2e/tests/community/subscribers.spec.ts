import { test, expect, type APIRequestContext } from "@playwright/test";
import {
  authenticatedContext,
  primaryUser,
  secondaryUser,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
} from "../../fixtures/auth";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

// The self-subscription test addresses the account it is signed in as, so it
// needs the name as well as the session. It was reading an undefined USER_A,
// which is a ReferenceError at run time — the file is not covered by
// type-check, whose project is src/** only.
const USER_A = primaryUser;
const USER_B = secondaryUser;

let userAContext: APIRequestContext;
let userBContext: APIRequestContext;

// Deliberately unguarded. Both sessions are written by global setup, which
// fails the whole run if a login fails; a try/catch here turned that into a
// console line and left every test below to skip itself on a null context.
// That is how this tier came to report green while nothing in it ran.
test.beforeAll(async () => {
  userAContext = await authenticatedContext(PRIMARY_STORAGE_STATE);
  userBContext = await authenticatedContext(SECONDARY_STORAGE_STATE);
});

test.afterAll(async () => {
  if (userAContext) await userAContext.dispose();
  if (userBContext) await userBContext.dispose();
});

test.describe("User Subscribers API", () => {
  test("should subscribe to a user", async () => {
    // User A subscribes to User B
    const response = await userAContext.post(
      `${API_URL}/v1/users/${USER_B.username}/subscribers`,
    );

    // 201 Created or 409 if already subscribed
    expect([201, 409]).toContain(response.status());
  });

  test("should get subscribers list", async ({ request }) => {
    // Get subscribers of User B (public endpoint)
    const response = await request.get(
      `${API_URL}/v1/users/${USER_B.username}/subscribers`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test("should check subscription status", async () => {
    // Check if User A is subscribed to User B
    const response = await userAContext.get(
      `${API_URL}/v1/users/${USER_B.username}/subscribers/me`,
    );

    // 200 with subscription data or 204 if not subscribed
    expect([200, 204]).toContain(response.status());
  });

  test("should unsubscribe from a user", async () => {
    // User A unsubscribes from User B
    const response = await userAContext.delete(
      `${API_URL}/v1/users/${USER_B.username}/subscribers`,
    );

    // 204 No Content or 404 if not subscribed
    expect([204, 404]).toContain(response.status());
  });

  test("should not allow subscribing to yourself", async () => {
    // User A tries to subscribe to themselves
    const response = await userAContext.post(
      `${API_URL}/v1/users/${USER_A.username}/subscribers`,
    );

    expect(response.status()).toBe(403);
  });

  test("should require authentication to subscribe", async ({ request }) => {
    const response = await request.post(
      `${API_URL}/v1/users/${USER_B.username}/subscribers`,
    );

    expect(response.status()).toBe(401);
  });
});
