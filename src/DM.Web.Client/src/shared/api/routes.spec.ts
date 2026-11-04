/**
 * @vitest-environment node
 */

/**
 * Every literal address this client asks for is one the server answers, as
 * published in artifacts/openapi-routes.json by
 * OpenApiContractShould.MatchTheCommittedRoutesSnapshot. A path built from a
 * variable is skipped; an interpolation becomes {param}.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, existsSync } from "fs";
import { dirname, join, resolve, relative } from "path";
import { fileURLToPath } from "url";
import { CLIENT_SRC, collect } from "@/shared/lib/testing/sourceTree";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..", "..", "..");
const snapshotPath = join(repoRoot, "artifacts", "openapi-routes.json");

/** Method names on the Api client that take a path as their first argument. */
const CALLS = ["get", "post", "postFile", "put", "patch", "delete"] as const;

/** How each of those maps onto an HTTP method in the snapshot. */
const METHOD: Record<string, string> = {
  get: "GET",
  post: "POST",
  postFile: "POST",
  put: "PUT",
  patch: "PATCH",
  delete: "DELETE",
};

type Call = { file: string; method: string; path: string };

/**
 * The calls notepadEndpoints(base) makes, in its order: the five notepads are
 * asked for through it, so the base is read at the call sites and expanded.
 */
const NOTEPAD_CALLS = [
  { method: "GET", suffix: "" },
  { method: "GET", suffix: "/{param}" },
  { method: "POST", suffix: "" },
  { method: "PATCH", suffix: "/{param}" },
  { method: "DELETE", suffix: "/{param}" },
];

const sourceFiles = (): string[] =>
  collect(
    CLIENT_SRC,
    (name) =>
      (name.endsWith(".ts") || name.endsWith(".vue")) &&
      !name.endsWith(".spec.ts"),
  );

/** An Api call reduced to its address, interpolations as placeholders. */
function callsIn(file: string): Call[] {
  const source = readFileSync(file, "utf8");
  const calls: Call[] = [];

  // A prefix kept as a field of a named class, substituted rather than read
  // as a parameter.
  const fields = new Map<string, string>();
  for (const field of source.matchAll(
    /(?:private|public|readonly)\s+(\w+)\s*=\s*["']([^"']+)["']/g,
  )) {
    fields.set(field[1], field[2]);
  }

  // A prefix constant at the top level of the module, at the head of the
  // address only: further along a constant is a path parameter.
  const constants = new Map<string, string>();
  for (const constant of source.matchAll(
    /^(?:export\s+)?const\s+(\w+)\s*=\s*["']([^"']+)["']\s*;?\s*$/gm,
  )) {
    constants.set(constant[1], constant[2]);
  }

  // The type argument nests (Api.get<ListEnvelope<X>>): matched to two levels.
  const generic = String.raw`<[^<>]*(?:<[^<>]*(?:<[^<>]*>)?[^<>]*>)?[^<>]*>`;
  const pattern = new RegExp(
    String.raw`\bApi\.(${CALLS.join("|")})\s*(?:${generic})?\s*\(\s*((?:\$\{[^}]*\}|[^,)])+)`,
    "g",
  );

  for (const match of source.matchAll(pattern)) {
    const [, call, rawArgument] = match;
    const argument = rawArgument.trim();
    const literal =
      /^"([^"]*)"$/.exec(argument)?.[1] ??
      /^'([^']*)'$/.exec(argument)?.[1] ??
      /^`([^`]*)`$/.exec(argument)?.[1];

    if (literal === undefined) continue;

    // A known field first, then whatever is left is a path parameter; the
    // snapshot spells those {param}.
    const path = literal
      .replace(
        /\$\{\s*this\.(\w+)\s*\}/g,
        (whole, name) => fields.get(name) ?? whole,
      )
      .replace(
        /^\$\{\s*(\w+)\s*\}/,
        (whole, name) => constants.get(name) ?? whole,
      )
      .replace(/\$\{[^}]*\}/g, "{param}")
      .split("?")[0];
    calls.push({ file: relative(repoRoot, file), method: METHOD[call], path });
  }

  for (const match of source.matchAll(
    /\bnotepadEndpoints\(\s*(`[^`]*`|"[^"]*"|'[^']*')/g,
  )) {
    const base = match[1].slice(1, -1).replace(/\$\{[^}]*\}/g, "{param}");
    for (const { method, suffix } of NOTEPAD_CALLS) {
      calls.push({
        file: relative(repoRoot, file),
        method,
        path: base + suffix,
      });
    }
  }

  return calls;
}

/** The snapshot, with every parameter name flattened the same way. */
function publishedRoutes(): Set<string> {
  const raw: string[] = JSON.parse(readFileSync(snapshotPath, "utf8"));
  return new Set(
    raw.map((route) => {
      const [method, path] = route.split(" ");
      return `${method} ${path.replace(/\{[^}]*\}/g, "{param}")}`;
    }),
  );
}

describe("client routes", () => {
  it("has the published routes to compare against", () => {
    expect(
      existsSync(snapshotPath),
      "artifacts/openapi-routes.json is written by the API integration suite; run it once",
    ).toBe(true);
    expect(publishedRoutes().size).toBeGreaterThan(100);
  });

  it("asks for nothing the API does not serve", () => {
    const published = publishedRoutes();
    const unknown: string[] = [];
    let checked = 0;

    // The whole client: calls are written in components too.
    for (const file of sourceFiles()) {
      for (const call of callsIn(file)) {
        checked++;
        const address = `${call.method} /v1/${call.path}`;
        if (!published.has(address)) {
          unknown.push(`${call.file}: ${address}`);
        }
      }
    }

    // A gate that finds no calls passes by checking nothing: the floor sits
    // just under the real count.
    expect(checked).toBeGreaterThan(330);
    expect(unknown).toEqual([]);
  });
});
