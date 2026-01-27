import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [vue()],
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
        // Minimum coverage thresholds (can be gradually increased)
        lines: 10,
        functions: 10,
        branches: 10,
        statements: 10,
      },
    },
  },
  css: {
    preprocessorOptions: {
      sass: {
        additionalData: `
          @import "@/assets/styles/Variables"
          @import "@/assets/styles/Layout"
          @import "@/assets/styles/Themes"
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
          'vue-vendor': ['vue', 'vue-router', 'pinia'],
          // TipTap editor - загружается только на chat/messenger
          // @tiptap/pm исключён - имеет особую структуру пакета
          'tiptap': [
            '@tiptap/vue-3',
            '@tiptap/starter-kit',
            '@tiptap/extension-link',
            '@tiptap/extension-image',
            '@tiptap/extension-underline',
            '@tiptap/extension-placeholder',
            '@tiptap/extension-code-block',
            '@tiptap/extension-bubble-menu',
          ],
          // SignalR - загружается для realtime
          'signalr': ['@microsoft/signalr'],
        },
      },
    },
    // Увеличим лимит предупреждения о размере chunk
    chunkSizeWarningLimit: 500,
  },
});
