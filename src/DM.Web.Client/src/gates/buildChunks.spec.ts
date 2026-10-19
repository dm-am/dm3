/**
 * @vitest-environment node
 */

import { describe, it, expect } from "vitest";
import { existsSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";

// src -> DM.Web.Client
const CLIENT_ROOT = resolve(CLIENT_SRC, "..");

/**
 * The entry chunk is what every visitor downloads before anything renders, so
 * what lands in it is a decision and not an accident of file layout: the game
 * and blog detail stores live in modules of their own, apart from the list
 * stores the sidebar imports statically.
 *
 * Both rules walk the static import graph from the entry; `import()` is a
 * chunk boundary and is not followed. "Reached from the entry" is not "in the
 * entry chunk": a barrel re-export nobody uses is shaken out. So the first rule
 * asks who uses the binding rather than who can reach the file, and the second
 * keeps the declaration alone in its module, which the barrel needs to shake it.
 */
const ENTRY = join(CLIENT_SRC, "app/main.ts");

interface Edge {
  specifier: string;
  /** A re-export is a name a barrel offers; the bundler drops the unasked. */
  reExport: boolean;
}

/** Static edges only. `import(...)` is a chunk boundary and is left out. */
function staticEdgesOf(file: string): Edge[] {
  const text = readFileSync(file, "utf8");
  const edges: Edge[] = [];
  const from = /(?:^|\n)\s*(import|export)[\s\S]*?from\s*["']([^"']+)["']/g;
  const bare = /(?:^|\n)\s*import\s*["']([^"']+)["']/g;
  for (const match of text.matchAll(from))
    edges.push({ specifier: match[2], reExport: match[1] === "export" });
  for (const match of text.matchAll(bare))
    edges.push({ specifier: match[1], reExport: false });
  return edges;
}

/** Resolves the way vite does for this project: "@" is src, plus extensions. */
function resolveSpecifier(from: string, specifier: string): string | null {
  if (!specifier.startsWith(".") && !specifier.startsWith("@/")) return null;
  const base = specifier.startsWith("@/")
    ? join(CLIENT_SRC, specifier.slice(2))
    : resolve(dirname(from), specifier);
  const candidates = [
    base,
    `${base}.ts`,
    `${base}.vue`,
    join(base, "index.ts"),
    join(base, "index.vue"),
  ];
  return (
    candidates.find((path) => existsSync(path) && statSync(path).isFile()) ??
    null
  );
}

/**
 * Modules the entry pulls in, and the ones it merely offers.
 *
 * `reached` is the whole static graph, barrels included. `pinned` is the part
 * of it some reached module *imports* rather than re-exports, which is the set
 * whose code the entry chunk actually has to carry: a sidebar block imports
 * `@/entities/game`, so that barrel names `model/detailsStore` to the graph,
 * and that re-export is exactly what the bundler drops.
 */
function walkFromEntry(): { reached: string[]; pinned: Set<string> } {
  const reached = new Set<string>();
  const pinned = new Set<string>();
  const queue = [ENTRY];
  while (queue.length > 0) {
    const file = queue.pop()!;
    if (reached.has(file)) continue;
    reached.add(file);
    for (const edge of staticEdgesOf(file)) {
      const resolved = resolveSpecifier(file, edge.specifier);
      if (resolved === null) continue;
      if (!edge.reExport) pinned.add(resolved);
      if (!reached.has(resolved)) queue.push(resolved);
    }
  }
  return { reached: [...reached], pinned };
}

const named = (file: string) => relative(CLIENT_ROOT, file).replace(/\\/g, "/");

const storesDeclaredIn = (file: string): string[] =>
  [
    ...readFileSync(file, "utf8").matchAll(/defineStore\(\s*["']([^"']+)["']/g),
  ].map((match) => match[1]);

describe("the entry chunk", () => {
  const { reached, pinned } = walkFromEntry();

  it("asks for no zone store the visitor has not entered", () => {
    // `import { useGameDetailsStore }`, not `export { … } from`: a re-export is
    // what the bundler drops, an import is what pins the module to the chunk.
    const askers = [...pinned]
      .filter((file) =>
        /(?:^|\n)\s*import[\s\S]{0,400}?\buse\w+DetailsStore\b[\s\S]{0,200}?from\s*["']/.test(
          readFileSync(file, "utf8"),
        ),
      )
      .map(named);

    expect(
      askers,
      "a zone store is imported by a module the entry pulls in, so it downloads for every visitor: the zone's own screens and its sidebar panel are lazy, and only they may name it",
    ).toEqual([]);
  });

  it("keeps a zone store alone in its module", () => {
    // Over the whole reached graph and not just the pinned part: a barrel can
    // only drop a re-export whose module the asked-for name does not need, so
    // one file holding both stores puts the zone back in the entry the moment
    // anything there asks for the list store.
    const shared = reached
      .filter((file) => {
        const stores = storesDeclaredIn(file);
        return (
          stores.length > 1 && stores.some((name) => /Details$/.test(name))
        );
      })
      .map(named);

    expect(
      shared,
      "a zone store sharing a module with another store rides into whatever chunk that other store is pulled into — the game and blog lists are drawn by sidebar blocks the entry loads, and that is how the whole zone got there",
    ).toEqual([]);
  });

  // Without this the pair above passes over an empty set the day the walk stops
  // resolving anything: no module reached, nothing pinned, nothing to report.
  it("is a graph and not an empty set", () => {
    expect(reached.length).toBeGreaterThan(200);
    expect(pinned.size).toBeGreaterThan(100);
    expect(
      reached.filter((file) => storesDeclaredIn(file).length > 0).length,
    ).toBeGreaterThan(3);
  });
});
