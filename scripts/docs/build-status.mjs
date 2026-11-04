#!/usr/bin/env node
// Counts the state of the site from the tree and writes docs/STATUS.md.
//
//   node scripts/docs/build-status.mjs           writes the document
//   node scripts/docs/build-status.mjs --check   writes nothing; exit 1 and a
//                                                diff when the file differs
//
// Nothing here starts the stand or a browser. What is read, and how:
//
//   by the TypeScript compiler (the client's own, src/DM.Web.Client/node_modules)
//     - the route table of src/DM.Web.Client/src/app/providers/router.ts and
//       the route modules of routes/ beside it
//     - the states of scripts/visual/states.ts and the roles of roles.ts
//
//   as text
//     - the syntax of a route path (vue-router's, compiled to a matcher)
//     - artifacts/openapi-routes.json, one "METHOD /v1/path" per entry
//
// StatusDocumentShould runs --check, so the document cannot drift from the tree.
import { writeFileSync } from "node:fs";
import { join } from "node:path";
import { ROOT, loadTypeScript, read } from "./lib/repo.mjs";
import { readRoutes } from "./lib/router.mjs";
import { routeResolver } from "./lib/matcher.mjs";
import { readFrames } from "./lib/frames.mjs";
import { readEndpoints } from "./lib/endpoints.mjs";
import { render } from "./lib/render.mjs";
import { lineDiff } from "./lib/diff.mjs";

const TARGET = join(ROOT, "docs", "STATUS.md");

function collect() {
  const ts = loadTypeScript();
  const { rows, unread: unreadRecords } = readRoutes(ts);
  const resolve = routeResolver(rows);
  // A state opening a redirect photographs where the redirect leads.
  const byName = new Map(rows.filter((row) => row.name).map((row) => [row.name, row]));
  const frames = readFrames(ts, (address) => {
    const row = resolve(address);
    return row?.redirect?.name ? byName.get(row.redirect.name) : row;
  });
  return { rows, unreadRecords, frames, endpoints: readEndpoints() };
}

function current() {
  try {
    return read(TARGET);
  } catch {
    return "";
  }
}

const counted = render(collect());

if (process.argv.includes("--check")) {
  const committed = current();
  if (committed !== counted) {
    process.stdout.write(`${lineDiff(committed, counted).join("\n")}\n`);
    process.exit(1);
  }
} else {
  writeFileSync(TARGET, counted);
}
