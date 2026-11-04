import { defineConfig, devices } from "@playwright/test";
import { APP_BASE_URL, PREVIEW_PORT } from "./e2e/fixtures/auth";

/**
 * An external target under test (staging, a container, a manually started
 * server). Set it and the config uses it as-is instead of building anything.
 */
const externalBaseUrl = process.env.E2E_BASE_URL;

const baseURL = APP_BASE_URL;

/** The one layout of the site is the one of this window. */
const WINDOW = { width: 1920, height: 911 };

export default defineConfig({
  testDir: "./e2e/tests",
  // Sign-in happens once per run and is saved into storageState.
  globalSetup: "./e2e/fixtures/global-setup.ts",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  // A machine-readable report next to the human-readable one: with retries: 2 a
  // test that passes on the third attempt leaves the job green, and the only trace
  // of the flake was an HTML artifact somebody had to download and open. The CI
  // step prints the list of flakes into the run summary from this file.
  reporter: [
    ["html"],
    ["list"],
    ["json", { outputFile: "playwright-report/results.json" }],
  ],
  use: {
    baseURL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
  },
  projects: [
    // Desktop browsers, in the one window the site is laid out for.
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"], viewport: WINDOW },
    },
    {
      name: "firefox",
      use: { ...devices["Desktop Firefox"], viewport: WINDOW },
    },
    { name: "webkit", use: { ...devices["Desktop Safari"], viewport: WINDOW } },
    {
      name: "edge",
      use: { ...devices["Desktop Edge"], channel: "msedge", viewport: WINDOW },
    },
  ],
  // Serve a build of this working tree on PREVIEW_PORT: a dev server measures
  // whatever it has transformed so far, an occupied port would adopt a stranger
  // (hence reuseExistingServer: false), and --strictPort keeps the app on the
  // port the API allows. The cost is one build per run.
  ...(externalBaseUrl
    ? {}
    : {
        webServer: {
          command: `npm run build && npm run preview -- --port ${PREVIEW_PORT} --strictPort`,
          url: baseURL,
          reuseExistingServer: false,
          timeout: 300_000,
        },
      }),
});
