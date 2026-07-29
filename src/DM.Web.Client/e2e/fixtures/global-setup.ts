import { request } from "@playwright/test";
import { mkdirSync } from "fs";
import { dirname } from "path";
import {
  API_BASE_URL,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
  primaryUser,
  secondaryUser,
} from "./auth";

/**
 * Log in once per run and save the session cookies to disk; every spec builds
 * its request context from those files instead of authenticating itself.
 *
 * This is not only tidiness. The login endpoint is rate-limited to 5 requests
 * per minute, and twelve spec files logging in from parallel workers hit 429
 * within seconds — which is how the suite reacted the moment its swallowed
 * login errors were made loud. Two logins per run stay far under the limit.
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

  mkdirSync(dirname(storagePath), { recursive: true });
  await context.storageState({ path: storagePath });
  await context.dispose();
}

export default async function globalSetup() {
  await saveSession(primaryUser, PRIMARY_STORAGE_STATE);
  await saveSession(secondaryUser, SECONDARY_STORAGE_STATE);
}
