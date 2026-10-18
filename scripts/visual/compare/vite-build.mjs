// One build of a materialized client tree, run as a child of build.mjs so
// that no state of a previous build survives into it. Takes one JSON argument:
// { client, tree, outDir, minify, manifest }.

import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import { pathToFileURL } from "node:url";

const { client, tree, outDir, minify, manifest } = JSON.parse(process.argv[2]);

// The vite the client has installed, found from the client and not from this
// script: scripts/ has no node_modules of its own.
const require = createRequire(join(client, "package.json"));
const vitePackage = require.resolve("vite/package.json");
const { exports } = JSON.parse(readFileSync(vitePackage, "utf8"));
const { build } = await import(
  pathToFileURL(join(dirname(vitePackage), exports["."])).href
);

await build({
  root: tree,
  configFile: join(tree, "vite.config.ts"),
  mode: "production",
  logLevel: "warn",
  clearScreen: false,
  build: {
    outDir,
    emptyOutDir: true,
    reportCompressedSize: false,
    // Undefined keeps what the config says; false switches the minifier off.
    minify: minify ? undefined : false,
    cssMinify: minify ? undefined : false,
    manifest: manifest || undefined,
    rollupOptions: {
      output: {
        entryFileNames: "assets/[name].js",
        chunkFileNames: "assets/[name].js",
        assetFileNames: "assets/[name][extname]",
      },
    },
  },
});
