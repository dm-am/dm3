import { defineConfig, devices } from "@playwright/test";

/**
 * Port of the preview server the suite runs against.
 *
 * Not a free choice: the API validates Origin twice (CORS allowlist and the
 * CSRF middleware), and its allowlist holds 5173, 5174 and 8080 only. On any
 * other port every request from the page is rejected and the whole tier reads
 * red for a reason that has nothing to do with the tests.
 */
const PREVIEW_PORT = 5174;

/**
 * An external target under test (staging, a container, a manually started
 * server). Set it and the config uses it as-is instead of building anything.
 */
const externalBaseUrl = process.env.E2E_BASE_URL;

const baseURL = externalBaseUrl || `http://localhost:${PREVIEW_PORT}`;

export default defineConfig({
  testDir: "./e2e/tests",
  // Логин выполняется один раз за прогон и сохраняется в storageState:
  // эндпоинт входа ограничен пятью запросами в минуту, а спеки логинились
  // каждая сама из параллельных воркеров.
  globalSetup: "./e2e/fixtures/global-setup.ts",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  // Машиночитаемый отчет рядом с человекочитаемым: с retries: 2 тест, прошедший
  // с третьей попытки, оставляет джобу зеленой, и единственным следом флейка был
  // HTML-артефакт, который надо скачать и открыть. Из этого файла шаг CI печатает
  // список флейков в сводку прогона.
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
    // Desktop browsers
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "firefox", use: { ...devices["Desktop Firefox"] } },
    { name: "webkit", use: { ...devices["Desktop Safari"] } },
    { name: "edge", use: { ...devices["Desktop Edge"], channel: "msedge" } },

    // Mobile phones
    { name: "mobile-chrome", use: { ...devices["Pixel 7"] } },
    { name: "mobile-safari", use: { ...devices["iPhone 15"] } },
    { name: "galaxy", use: { ...devices["Galaxy S24"] } },

    // Tablets
    { name: "ipad", use: { ...devices["iPad Pro 11"] } },
    { name: "android-tablet", use: { ...devices["Galaxy Tab S9"] } },
  ],
  // Build the bundle, then serve that build. Three properties matter, and the
  // tier lost all three when it ran `npm run dev` with reuseExistingServer:
  //
  // 1. It is a build. A dev server compiles on demand, so what the suite
  //    measures depends on which modules that particular process has already
  //    transformed. A `vite preview` serves the same immutable files to
  //    everyone, and `npm run build` type-checks them on the way in.
  // 2. It is this working tree's build. Reusing whatever already listens on
  //    the port adopts a stranger: the run that produced the "37 of 38 red"
  //    number was measured against a long-running dev server whose injected
  //    client threw `__WS_TOKEN__ is not defined` before the app mounted, so
  //    38 tests were asserting against a white screen and the register drew
  //    conclusions about the spec from it. Hence reuseExistingServer: false —
  //    an occupied port now fails the run loudly instead of silently
  //    substituting an unknown build.
  // 3. It is on PREVIEW_PORT. --strictPort keeps vite from sliding to the next
  //    free port, which would leave the page alive but every API call blocked
  //    by CORS — red tests, healthy app.
  //
  // Cost: one build per run. That is the price of a reproducible measurement.
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
