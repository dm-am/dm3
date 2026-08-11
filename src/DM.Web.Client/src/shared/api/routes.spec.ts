/**
 * @vitest-environment node
 */

/**
 * Every address this client asks for is one the server answers.
 *
 * The models in this folder are held to the published schemas by
 * contract.spec.ts; nothing held the addresses. The account security section
 * spent its whole life calling `account/security`, which no controller has ever
 * served, and rendered empty without a word — its loader leaves the list alone
 * when the request fails, so a 404 looked exactly like "no events yet".
 *
 * The server side of the handshake is DM.Web.API.IntegrationTests
 * OpenApiContractShould.MatchTheCommittedRoutesSnapshot, which writes every
 * published "METHOD /v1/path" to artifacts/openapi-routes.json. This test reads
 * that file and holds every literal path in the api folders to it.
 *
 * Only literal paths are checked. A path assembled from a variable is skipped
 * and counted: the alternative is evaluating the module, and a call whose whole
 * address is computed is rare enough to be worth the hole. Interpolated
 * segments (`games/${id}/rooms`) are literal enough — the expression becomes a
 * `{param}` placeholder, which is the shape the snapshot stores.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, existsSync, readdirSync, statSync } from "fs";
import { dirname, join, resolve, relative } from "path";
import { fileURLToPath } from "url";

const here = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(here, "..", "..");
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

function sourceFiles(dir: string, found: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      if (entry !== "node_modules") sourceFiles(full, found);
    } else if (entry.endsWith(".ts") && !entry.endsWith(".spec.ts")) {
      found.push(full);
    }
  }
  return found;
}

/**
 * `Api.get("chats", ...)` and `Api.post(`games/${id}/rooms`, ...)`, reduced to
 * the address with its interpolations turned into placeholders.
 */
function callsIn(file: string): { calls: Call[]; skipped: number } {
  const source = readFileSync(file, "utf8");
  const calls: Call[] = [];
  let skipped = 0;

  // `${this.basePath}` is not a path parameter, it is the prefix spelled once
  // at the top of the class. Substituted rather than blanked: read as a
  // parameter it turns six real addresses into /v1/{param}/unread and friends,
  // which match nothing and would have to be excused by hand.
  const fields = new Map<string, string>();
  for (const field of source.matchAll(
    /(?:private|public|readonly)\s+(\w+)\s*=\s*["']([^"']+)["']/g,
  )) {
    fields.set(field[1], field[2]);
  }

  // The type argument nests — Api.get<ListEnvelope<SecurityEvent>> is the
  // ordinary shape here, not the exception. Matched to two levels, because a
  // pattern that stops at the first ">" does not merely mis-read those calls,
  // it fails to match them at all: the first draft of this gate skipped every
  // enveloped list, which is most of the client, and passed while the wrong
  // address it was written for sat in the file.
  const generic = String.raw`<[^<>]*(?:<[^<>]*(?:<[^<>]*>)?[^<>]*>)?[^<>]*>`;
  const pattern = new RegExp(
    String.raw`\bApi\.(${CALLS.join("|")})\s*(?:${generic})?\s*\(\s*([^,)]+)`,
    "g",
  );

  for (const match of source.matchAll(pattern)) {
    const [, call, rawArgument] = match;
    const argument = rawArgument.trim();
    const literal =
      /^"([^"]*)"$/.exec(argument)?.[1] ??
      /^'([^']*)'$/.exec(argument)?.[1] ??
      /^`([^`]*)`$/.exec(argument)?.[1];

    if (literal === undefined) {
      skipped++;
      continue;
    }

    // A known field first, then whatever is left is a path parameter; the
    // snapshot spells those {param}.
    const path = literal
      .replace(
        /\$\{\s*this\.(\w+)\s*\}/g,
        (whole, name) => fields.get(name) ?? whole,
      )
      .replace(/\$\{[^}]*\}/g, "{param}")
      .split("?")[0];
    calls.push({ file: relative(repoRoot, file), method: METHOD[call], path });
  }

  return { calls, skipped };
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
  const apiDirs = [
    join(clientRoot, "entities"),
    join(clientRoot, "shared", "api"),
  ].filter(existsSync);

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

    for (const dir of apiDirs) {
      for (const file of sourceFiles(dir)) {
        const { calls } = callsIn(file);
        for (const call of calls) {
          checked++;
          const address = `${call.method} /v1/${call.path}`;
          if (!published.has(address)) {
            unknown.push(`${call.file}: ${address}`);
          }
        }
      }
    }

    // A gate that stops finding calls passes by checking nothing.
    expect(checked).toBeGreaterThan(150);
    expect(unknown).toEqual([]);
  });
});
