// Preview-only Vite config — adds /v1 proxy so the frontend running on a
// non-5173 port can still talk to the backend without CORS issues.
// Used by the Claude Preview launch config.
import { fileURLToPath, URL } from "node:url";
import { defineConfig, mergeConfig } from "vite";
import baseConfig from "./vite.config";

export default defineConfig((env) => {
  const resolved =
    typeof baseConfig === "function" ? baseConfig(env) : baseConfig;
  return mergeConfig(resolved, {
    server: {
      port: 5174,
      strictPort: false,
      proxy: {
        "/v1": {
          target: "http://localhost:5000",
          changeOrigin: true,
        },
        // The SignalR hub. Without it the negotiate request is answered by Vite
        // with a 404 and realtime silently degrades to the 30s poll, which reads
        // as "working, just slow".
        "/whatsup": {
          target: "http://localhost:5000",
          changeOrigin: true,
          ws: true,
        },
      },
    },
    resolve: {
      alias: {
        "@": fileURLToPath(new URL("./src", import.meta.url)),
      },
    },
  });
});
