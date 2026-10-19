import { fileURLToPath, URL } from "node:url";

// From vitest, not from vite: the `test` block below is vitest's own.
import { defineConfig } from "vitest/config";
import vue from "@vitejs/plugin-vue";
import { deprecations } from "sass";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    strictPort: true,
  },
  optimizeDeps: {
    exclude: ["vue-final-modal"],
  },
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  test: {
    environment: "jsdom",
    root: "src/",
    globals: true,
    // The convention gates walk the whole source tree and run past the 5s
    // default on a busy machine (the push hook makes it busy); the ceiling is
    // raised for all of them, under the 30s the heaviest scans declare.
    testTimeout: 15_000,
    // What a test writes to the console is printed only when it fails.
    silent: "passed-only",
    coverage: {
      provider: "v8",
      reporter: ["text", "html", "json-summary"],
      reportsDirectory: "../coverage",
      include: ["**/*.{ts,vue}"],
      exclude: [
        "**/*.spec.ts",
        // The harness beside a spec: its stand-ins and its data, not the app.
        "**/*.doubles.ts",
        "**/*.fixtures.ts",
        "**/node_modules/**",
        "**/dist/**",
        "main.ts",
      ],
      thresholds: {
        // A ratchet, not a floor to duck under: raise after a gain, never lower
        // after a miss. The denominator is the whole source tree, and each
        // number sits about 1.7 points under what a full run measures, so the
        // gate fails on a real loss and not on one large untested component.
        lines: 82.3,
        functions: 74.9,
        branches: 74.2,
        statements: 80.9,
      },
    },
  },
  css: {
    preprocessorOptions: {
      sass: {
        // Every deprecation the compiler reports is an error, not a line in
        // the log. A warning scrolls past in the dev server and the build and
        // fails nothing, so the one "/" division that slipped in sat there
        // until someone read the terminal; the next major release of Sass
        // removes the syntax, and the build that breaks then is the one that
        // upgrades.
        // Active ones only: an obsolete id makes the compiler warn about the
        // option itself, and a future one is not reported until enabled.
        fatalDeprecations: Object.values(deprecations).filter(
          (deprecation) => deprecation.status === "active",
        ),
        // The shared layer, in front of every stylesheet the project compiles:
        // a component asks for $text or +card without saying where they live.
        // `as *` keeps the names the files already write; no two of these
        // declare the same member (Sass refuses to compile if two did). Each
        // must emit no rule of its own: it would be re-emitted into every
        // scoped stylesheet (InputsGlobal.sass records what that cost).
        additionalData: `@use "@/assets/styles/Variables" as *
@use "@/assets/styles/Themes" as *
@use "@/assets/styles/Surfaces" as *
`,
      },
    },
  },
  build: {
    // Dependencies that change on their own schedule, in chunks of their own so
    // a release of the app does not invalidate their cache entry.
    rollupOptions: {
      output: {
        codeSplitting: {
          groups: [
            // The framework: the largest thing a returning reader never re-downloads.
            {
              name: "vue-vendor",
              test: /node_modules[\\/](vue|@vue|vue-router|pinia)[\\/]/,
            },
            // The editor engine: the views that only read text never download it.
            // @tiptap/pm is left out: that package has a structure of its own.
            {
              name: "tiptap",
              test: /node_modules[\\/]@tiptap[\\/](?!pm[\\/])/,
            },
            // The realtime transport, for the chats and the notification bell only.
            {
              name: "signalr",
              test: /node_modules[\\/]@microsoft[\\/]signalr[\\/]/,
            },
          ],
        },
      },
    },
  },
});
