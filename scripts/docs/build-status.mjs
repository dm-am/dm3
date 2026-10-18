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
//     - the route table of src/DM.Web.Client/src/app/providers/router.ts
//     - the states of scripts/visual/states.ts and the roles of roles.ts
//     - every .ts of the client and the script blocks of every .vue: Api calls,
//       `name:` properties and address literals
//     - the bound attributes of the .vue templates, once cut out as text
//
//   as text, by regular expressions, where the form is fixed
//     - the cut of a .vue file into script and template, and the attributes of
//       the template (a value is always in double quotes)
//     - the syntax of a route path (vue-router's, compiled to a matcher)
//     - artifacts/openapi-routes.json, one "METHOD /v1/path" per entry
//     - the API addresses the server writes into its answers, interpolated
//       strings starting with "/v1/" (lib/endpoints.mjs)
//     - the three shapes in which the server builds a link of a letter or a
//       notification (lib/links.mjs)
//     - the string literals and the walks of documents in the code lines of
//       test/DM.Architecture.Tests (lib/documents.mjs)
//
// StatusDocumentShould runs --check, so the document cannot drift from the tree.
import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { ROOT, loadTypeScript } from "./lib/repo.mjs";
import { readRoutes } from "./lib/router.mjs";
import { routeResolver } from "./lib/matcher.mjs";
import { readFrames } from "./lib/frames.mjs";
import { readClient } from "./lib/client.mjs";
import { matchEndpoints, readCalls, readEndpoints, readServerAddresses } from "./lib/endpoints.mjs";
import { readClientLinks, readServerLinks } from "./lib/links.mjs";
import { readDocuments } from "./lib/documents.mjs";
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
  const client = readClient(ts);
  const endpoints = readEndpoints();
  const { calls, unread: unreadCalls, opensServed } = readCalls(ts, client);
  // An address the server writes is a call only where the client opens such addresses.
  const addresses = opensServed.length > 0 ? readServerAddresses() : [];
  const { orphans, served, unmatched } = matchEndpoints(endpoints, calls, addresses);
  return {
    rows,
    unreadRecords,
    frames,
    endpoints,
    orphans,
    served,
    opensServed,
    unmatched,
    unreadCalls,
    clientLinks: readClientLinks(ts, client, rows, resolve),
    serverLinks: readServerLinks(rows, resolve),
    documents: readDocuments(),
  };
}

function current() {
  try {
    return readFileSync(TARGET, "utf8").replace(/\r\n/g, "\n");
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
