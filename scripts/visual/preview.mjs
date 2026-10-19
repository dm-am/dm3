// The client the frames are taken of: built from the tree into a directory of
// the run's own and served by vite preview on the one other port the API
// admits.

import { spawn } from "node:child_process";
import { createServer } from "node:net";
import { join } from "node:path";
import { fail, run } from "./refusal.mjs";
import { CLIENT, WORK } from "./tree.mjs";

export const DIST = join(WORK, "dist");

// The one origin besides the owner's dev server that the API admits in
// Development (src/DM.Web.API/Startup.cs), and the one e2e uses. Not a port
// to slide from: on any other one the API refuses every request by origin.
export const PREVIEW_PORT = 5174;

function portIsFree(port) {
  return new Promise((done) => {
    const probe = createServer();
    probe.once("error", () => done(false));
    probe.once("listening", () => probe.close(() => done(true)));
    probe.listen(port, "localhost");
  });
}

async function waitFor(url, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch {
      // not up yet
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  fail(`${url} did not answer in ${timeoutMs / 1000}s`);
}

// A build of this working tree into a directory of its own: dist/ belongs to
// whoever ran "npm run build" last, and the owner's dev server is not a build.
export function buildClient(tree) {
  const client = join(tree, "src", "DM.Web.Client");
  run(
    process.execPath,
    [
      join(client, "node_modules", "vite", "bin", "vite.js"),
      "build",
      "--outDir",
      DIST,
      "--emptyOutDir",
      "--logLevel",
      "warn",
    ],
    client,
  );
}

export async function startPreview() {
  // Refusing an occupied port rather than sliding to the next one: another
  // server there is a stranger's build, and on any other port the API rejects
  // every request by origin.
  if (!(await portIsFree(PREVIEW_PORT))) {
    fail(
      `port ${PREVIEW_PORT} is taken; stop whatever serves it (an e2e run?) and retry. ` +
        "The preview cannot move: the API lets a browser in from 5173 and 5174 only",
    );
  }
  const preview = spawn(
    process.execPath,
    [
      join(CLIENT, "node_modules", "vite", "bin", "vite.js"),
      "preview",
      "--outDir",
      DIST,
      "--port",
      String(PREVIEW_PORT),
      "--strictPort",
    ],
    { cwd: CLIENT, stdio: "ignore" },
  );
  try {
    await waitFor(`http://localhost:${PREVIEW_PORT}/`, 60_000);
  } catch (error) {
    // Left running, the server would keep this process from ever ending.
    preview.kill();
    throw error;
  }
  return preview;
}
