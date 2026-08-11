import { fileURLToPath, URL } from "node:url";

// From vitest, not from vite: the `test` block below is vitest's, and vite's own
// defineConfig does not know it. Typed by the wrong one it was an error nothing
// reported — tsconfig.config.json is the only project that includes this file,
// and no script type-checks it.
import { defineConfig } from "vitest/config";
import vue from "@vitejs/plugin-vue";

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
    coverage: {
      provider: "v8",
      reporter: ["text", "html", "json-summary"],
      reportsDirectory: "../coverage",
      include: ["**/*.{ts,vue}"],
      exclude: [
        "**/*.spec.ts",
        "**/*.test.ts",
        "**/node_modules/**",
        "**/dist/**",
        "main.ts",
        "env.d.ts",
      ],
      thresholds: {
        // Ratchet, not a floor to duck under. These sit just below the measured
        // numbers so the gate fails on a real loss — deleted tests, or a sizable
        // new file with none. They were once lowered to make CI pass, which is
        // the one thing that must never happen to them: raise after a gain,
        // never lower after a miss.
        //
        // The denominator is the whole source tree (include: **/*.{ts,vue}), so
        // one uncovered 500-line component costs about 0.6 points of lines.
        // Branches reads high because files with no tests contribute few branch
        // counters — lines and statements are the load-bearing numbers here.
        //
        // Measured on a full run of the suite (156 spec files, 1712 tests):
        // 33.78% lines and statements, 36.4% functions, 74.44% branches. The
        // measurement is written down for the same reason as in
        // scripts/check-coverage.sh: without it nobody can tell a ratchet that
        // was just raised from one that has stood still since the first audit —
        // which is what these numbers had done, sitting at roughly half of what
        // the suite actually covered and failing on nothing.
        //
        // The gap to the measurement is the backend's, 1.3 to 1.8 points: below
        // it the gate stops catching a real loss, above it a single large
        // untested component turns CI red.
        lines: 32,
        functions: 35,
        branches: 73,
        statements: 32,
      },
    },
  },
  css: {
    preprocessorOptions: {
      sass: {
        additionalData: `
          @import "@/assets/styles/Variables"
          @import "@/assets/styles/Breakpoints"
          @import "@/assets/styles/Layout"
          @import "@/assets/styles/Themes"
          @import "@/assets/styles/Surfaces"
        `,
      },
    },
  },
  build: {
    // Dependencies that change on their own schedule, split out of the app
    // chunk so a release of the app does not invalidate their cache entry.
    rollupOptions: {
      output: {
        manualChunks: {
          // The framework itself: changes a few times a year, is on every
          // address, and is the largest thing a returning reader never
          // re-downloads.
          "vue-vendor": ["vue", "vue-router", "pinia"],
          // The engine behind BBCodeEditor, in a chunk of its own rather than
          // in vendor. Not because few views need it: two dozen do (forum,
          // blogs, games, profile, moderation, support - anywhere text is
          // composed). Because the views that only READ text do not, and at
          // 361 KB raw it is the largest thing a reader can avoid downloading.
          // @tiptap/pm is left out: that package has a structure of its own.
          tiptap: [
            "@tiptap/vue-3",
            "@tiptap/starter-kit",
            "@tiptap/extension-link",
            "@tiptap/extension-image",
            "@tiptap/extension-underline",
            "@tiptap/extension-placeholder",
            "@tiptap/extension-code-block",
            "@tiptap/extension-bubble-menu",
          ],
          // The realtime transport: the chat, the global chat and the
          // notification bell need it, a reader who opens none of them does
          // not.
          signalr: ["@microsoft/signalr"],
        },
      },
    },
  },
});
