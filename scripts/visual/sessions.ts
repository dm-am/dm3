import { request } from "@playwright/test";
import { mkdirSync, writeFileSync } from "fs";
import { join } from "path";
import { ROLES, type Role } from "./roles";
import { resolveFixture } from "./fixture";

/**
 * Signs every role in once per run and saves the session the way the e2e
 * global setup does: the cookie the API checks and localStorage["user"] the
 * interface mounts from. One login per role, because each login is a write
 * (the last activity of the account) and the fewer of them, the fewer places
 * a run leaves its own clock on the page.
 */
async function saveSession(
  role: Role,
  app: string,
  api: string,
  directory: string,
) {
  const context = await request.newContext({
    extraHTTPHeaders: { Origin: app },
  });
  const response = await context.post(`${api}/v1/account/login`, {
    data: { email: role.email, password: role.password },
  });
  if (!response.ok()) {
    throw new Error(
      `login of ${role.email} failed: ${response.status()} ${await response.text()}. ` +
        (response.status() === 429
          ? 'The API\'s rate limiting is on: "powershell -File scripts/dm.ps1 e2e" recreates only the API, with the limits off, and the data stays.'
          : "Is the stand up and seeded? node scripts/visual/visual.mjs stand checks it."),
    );
  }
  const { user } = await response.json();
  const state = await context.storageState();
  await context.dispose();

  writeFileSync(
    join(directory, `${role.name}.json`),
    JSON.stringify({
      cookies: state.cookies,
      origins: [
        {
          origin: app,
          localStorage: [{ name: "user", value: JSON.stringify(user) }],
        },
      ],
    }),
  );
}

export default async function globalSetup() {
  const app = process.env.VISUAL_APP!;
  const api = process.env.VISUAL_API!;
  const directory = process.env.VISUAL_AUTH!;
  mkdirSync(directory, { recursive: true });

  for (const role of ROLES) {
    if (role.email) await saveSession(role, app, api, directory);
  }
  writeFileSync(
    join(directory, "fixture.json"),
    JSON.stringify(await resolveFixture(api, directory), null, 2),
  );
}
