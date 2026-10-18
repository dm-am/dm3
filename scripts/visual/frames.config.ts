import { defineConfig } from "@playwright/test";

// Started by visual.mjs, which builds the client, serves it on 5174 and passes
// everything below through the environment. Run by hand it has no server.
const env = (name: string): string => {
  const value = process.env[name];
  if (!value)
    throw new Error(`${name} is not set: run scripts/visual/visual.mjs`);
  return value;
};

const base = env("VISUAL_BASE");
const report = env("VISUAL_REPORT");
const diffing = env("VISUAL_MODE") === "diff";

export default defineConfig({
  testDir: ".",
  testMatch: "frames.spec.ts",
  globalSetup: "./sessions.ts",
  // One worker and the file's order: a state can leave a mark the next one
  // reads (a topic opened is a topic read), and only a fixed order makes two
  // runs see the same marks.
  workers: 1,
  fullyParallel: false,
  retries: 0,
  timeout: 180_000,
  // The frames of a difference go next to the report; a snapshot run has
  // nothing to keep.
  outputDir: diffing ? report : env("VISUAL_OUTPUT"),
  snapshotPathTemplate: `${base}/{projectName}/{arg}{ext}`,
  reporter: [["list"], ["./reporter.ts", { report, diffing }]],
  expect: {
    timeout: 20_000,
    toHaveScreenshot: {
      maxDiffPixels: 0,
      // Not a pixel may differ, but a pixel may differ by one unit of a
      // channel: the rasteriser draws an anti-aliased edge (the corner of a
      // rounded border) a unit lighter or darker from one browser launch to
      // the next on the same build, and at a threshold of 0 those frames
      // differed between two runs of one commit. 0.005 is the YIQ distance
      // that admits that unit and refuses two.
      threshold: 0.005,
      animations: "disabled",
      caret: "hide",
      scale: "device",
    },
  },
  use: {
    baseURL: env("VISUAL_APP"),
    headless: true,
    locale: "ru-RU",
    timezoneId: "Europe/Moscow",
    colorScheme: env("VISUAL_THEME") === "dark" ? "dark" : "light",
    trace: "off",
    screenshot: "off",
    video: "off",
  },
  projects: [
    {
      name: "desktop",
      use: { viewport: { width: 1920, height: 911 }, deviceScaleFactor: 1 },
    },
    {
      // A phone as e2e/fixtures/phone.ts emulates one: the viewport tag is
      // honoured, so the page is laid out at the width index.html gives it.
      name: "phone",
      use: {
        viewport: { width: 390, height: 844 },
        deviceScaleFactor: 3,
        isMobile: true,
        hasTouch: true,
      },
    },
  ],
});
