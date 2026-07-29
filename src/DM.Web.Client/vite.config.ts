import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vite";
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
        lines: 16,
        functions: 26,
        branches: 66,
        statements: 16,
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
    // Разделение vendor библиотек для лучшего кэширования
    rollupOptions: {
      output: {
        manualChunks: {
          // Vue core - меняется редко, хорошо кэшируется
          "vue-vendor": ["vue", "vue-router", "pinia"],
          // TipTap editor - загружается только на chat/messenger
          // @tiptap/pm исключен - имеет особую структуру пакета
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
          // SignalR - загружается для realtime
          signalr: ["@microsoft/signalr"],
        },
      },
    },
    // Увеличим лимит предупреждения о размере chunk
    chunkSizeWarningLimit: 500,
  },
});
