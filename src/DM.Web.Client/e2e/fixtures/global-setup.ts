import { request } from "@playwright/test";
import { mkdirSync, writeFileSync } from "fs";
import { dirname } from "path";
import {
  API_BASE_URL,
  APP_BASE_URL,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
  primaryUser,
  secondaryUser,
} from "./auth";

/**
 * Log in once per run and save the session to disk; every spec builds its
 * context from those files instead of authenticating itself.
 *
 * This is not only tidiness. The login endpoint is rate-limited to 5 requests
 * per minute, and twelve spec files logging in from parallel workers hit 429
 * within seconds — which is how the suite reacted the moment its swallowed
 * login errors were made loud. Two logins per run stay far under the limit.
 *
 * The session is two things, and saving one of them is what a browser reads as
 * a guest. The cookie is what the API checks. The interface checks
 * localStorage["user"]: the store is built from that key before any request
 * goes out, and everything gated on a viewer - the left sidebar, the vote
 * button, every route with requiresAuth - is mounted or not by the time the
 * server could answer. A request context has no localStorage to save, so the
 * file it wrote carried the cookie alone: the API answered as the signed-in
 * account while the page rendered for a stranger.
 */
async function saveSession(
  user: { email: string; password: string },
  storagePath: string,
) {
  const context = await request.newContext();
  const response = await context.post(`${API_BASE_URL}/v1/account/login`, {
    headers: { "Content-Type": "application/json" },
    data: { email: user.email, password: user.password },
  });

  if (!response.ok()) {
    const body = await response.text();
    throw new Error(
      `E2E global setup: login failed for ${user.email}: ${response.status()} - ${body}. ` +
        "Is the API up and the database seeded (scripts/dm.ps1 seed)?",
    );
  }

  // The same envelope the application unpacks, and the same key it writes it
  // under: entities/user/lib/session.ts hands data.user to the auth store,
  // which persists it as localStorage["user"]. Storing anything else here is a
  // fixture that signs in differently from the product.
  const { user: viewer } = await response.json();

  if (!viewer) {
    throw new Error(
      `E2E global setup: login for ${user.email} answered without a user. ` +
        "POST /v1/account/login is expected to return { user, preferences }.",
    );
  }

  const state = await context.storageState();
  await context.dispose();

  mkdirSync(dirname(storagePath), { recursive: true });
  writeFileSync(
    storagePath,
    JSON.stringify(
      {
        ...state,
        origins: [
          {
            origin: APP_BASE_URL,
            localStorage: [{ name: "user", value: JSON.stringify(viewer) }],
          },
        ],
      },
      null,
      2,
    ),
  );
}

export default async function globalSetup() {
  await saveSession(primaryUser, PRIMARY_STORAGE_STATE);
  await saveSession(secondaryUser, SECONDARY_STORAGE_STATE);
}
