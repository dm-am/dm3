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
    // Four dozen specs are convention gates that walk the whole source tree and
    // read every file; under vitest 4 they run past the 5s default whenever the
    // machine is busy, and the push hook makes it busy by design (it runs the
    // backend gates first). A timeout does not decide whether a gate is
    // satisfied, only how long it may take, so this raises the ceiling for all
    // of them instead of sprinkling per-file overrides that the next gate
    // forgets. Kept well under the per-file 30s the heaviest scans declare, so
    // a genuinely hung test still fails fast.
    testTimeout: 15_000,
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
        //
        // Measured on a full run of the suite (178 spec files, 1834 tests) on
        // vitest 4 / @vitest/coverage-v8 4: 32.67% lines, 31.83% statements,
        // 25.77% functions, 24.69% branches. The measurement is written down
        // for the same reason as in scripts/check-coverage.sh: without it
        // nobody can tell a ratchet that was just raised from one that has
        // stood still since the first audit — which is what these numbers had
        // done, sitting at roughly half of what the suite actually covered and
        // failing on nothing.
        //
        // The vitest 2 baseline (33.78% lines and statements, 36.4% functions,
        // 74.44% branches) is not comparable: coverage-v8 4 remaps through the
        // AST, and files no test loads now contribute every one of their
        // function and branch counters to the denominator instead of almost
        // none. Same suite, same sources — a different instrument. Branches no
        // longer reads high for the old artifact of a reason, so all four
        // numbers now carry weight.
        //
        // The gap to the measurement is the backend's, 1.3 to 1.8 points: below
        // it the gate stops catching a real loss, above it a single large
        // untested component turns CI red.
        lines: 31,
        functions: 24,
        branches: 23,
        statements: 30,
      },
    },
  },
  css: {
    preprocessorOptions: {
      sass: {
        // The shared layer, in front of every stylesheet the project compiles:
        // a component asks for $text or +card without saying where they live.
        //
        // `as *` and not a namespace. Under the module system a member is
        // reached through the name of the module that owns it, and namespacing
        // this injection would mean rewriting every reference in 308 style
        // blocks — a restyle, not a migration. `as *` keeps the names the
        // files already write. It is safe here because no two of these five
        // declare the same member; if two ever did, Sass would refuse to
        // compile rather than let one quietly win, which is more than @import
        // ever offered.
        //
        // Injecting a module is free: @use evaluates it once per compilation
        // and emits nothing, and none of the five emits a rule of its own.
        // That is a standing requirement of this list, not an accident — a
        // rule added to any of them would be re-emitted into all 308 scoped
        // stylesheets (InputsGlobal.sass records what that cost the last time
        // it happened).
        additionalData: `@use "@/assets/styles/Variables" as *
@use "@/assets/styles/Breakpoints" as *
@use "@/assets/styles/Layout" as *
@use "@/assets/styles/Themes" as *
@use "@/assets/styles/Surfaces" as *
`,
      },
    },
  },
  build: {
    // Dependencies that change on their own schedule, split out of the app
    // chunk so a release of the app does not invalidate their cache entry.
    // Rolldown (vite 8) dropped the object form of manualChunks; these are
    // the same three chunks expressed as codeSplitting groups, matched by
    // package path instead of by entry module list.
    rollupOptions: {
      output: {
        codeSplitting: {
          groups: [
            // The framework itself: changes a few times a year, is on every
            // address, and is the largest thing a returning reader never
            // re-downloads. @vue/* are the runtime packages vue re-exports.
            {
              name: "vue-vendor",
              test: /node_modules[\\/](vue|@vue|vue-router|pinia)[\\/]/,
            },
            // The engine behind BBCodeEditor, in a chunk of its own rather
            // than in vendor. Not because few views need it: two dozen do
            // (forum, blogs, games, profile, moderation, support - anywhere
            // text is composed). Because the views that only READ text do
            // not, and it is the largest thing a reader can avoid
            // downloading. A figure for it does not belong here: it moves on
            // every minor of tiptap and nobody would come back to correct it,
            // and the argument does not rest on the exact number anyway.
            // @tiptap/pm is left out: that package has a structure of its own.
            {
              name: "tiptap",
              test: /node_modules[\\/]@tiptap[\\/](?!pm[\\/])/,
            },
            // The realtime transport: the chat, the global chat and the
            // notification bell need it, a reader who opens none of them
            // does not.
            {
              name: "signalr",
              test: /node_modules[\\/]@microsoft[\\/]signalr[\\/]/,
            },
            // The QR encoder. One component reaches it by a dynamic import, so
            // it would be a chunk of its own without this entry; what the entry
            // buys is the name. Left alone, the bundler names the chunk after
            // the package's entry FILE - dist/index - and a network waterfall
            // then shows "dist-<hash>.js", which the next person reading it has
            // to open in order to find out what it is.
            {
              name: "qrcode",
              test: /node_modules[\\/]uqr[\\/]/,
            },
          ],
        },
      },
    },
  },
});
