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
      },
    },
    resolve: {
      alias: {
        "@": fileURLToPath(new URL("./src", import.meta.url)),
      },
    },
  });
});
