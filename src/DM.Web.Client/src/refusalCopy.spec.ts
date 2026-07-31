/**
 * @vitest-environment node
 */

/**
 * The wording of a refusal belongs to the server.
 *
 * A 403 is not one event. The API refuses with a sentence of its own in 68
 * places — "Вы в черном списке этого блога", "Аккаунт заблокирован", "Нельзя
 * подписаться на собственный блог" — and answers an authorization refusal with
 * the one constant title kept in DM.Domain.Core.Exceptions.RefusalMessage. The
 * response interceptor relays that title, so no module on the client has to
 * decide what a refusal says.
 *
 * When one decides anyway, its sentence and the server's end up on the same
 * screen: the poll form said "Недостаточно прав" in the form while the toast
 * above it said "Недостаточно прав для этого действия" — one event, two
 * wordings, which is the outcome CODE_STYLE names in "одно слово на одно
 * значение". Three forms carried such a copy, in the same shape, down to the
 * same shortened sentence.
 *
 * The check is textual and narrow on purpose: it holds the one refusal that had
 * four copies here. It cannot judge a module that invents some other sentence
 * for the same 403 — no scan can — but a copy is made from a copy in sight, and
 * this rule keeps the last one out of sight. Comments count: a copy of the
 * sentence in prose is the draft of the next copy in code. Spec files do not —
 * a test quotes the wording in order to assert on it (client.spec.ts does).
 *
 * The single place allowed to word it is the interceptor's own fallback, for a
 * 403 the error middleware never saw: the framework answers those with no body,
 * and the toast still has to say something.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
// src -> DM.Web.Client
const CLIENT_ROOT = resolve(HERE, "..");
const REPO_ROOT = resolve(CLIENT_ROOT, "..", "..");

const ACCESS_REFUSAL = "Недостаточно прав";
const INTERCEPTOR = "src/DM.Web.Client/src/shared/api/client.ts";

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

const sources = (dir: string): string[] => {
  const found: string[] = [];
  for (const name of readdirSync(dir)) {
    if (SKIP_DIRS.has(name)) continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      found.push(...sources(full));
      continue;
    }
    if (!/\.(ts|vue)$/.test(name)) continue;
    if (/\.(spec|test)\.ts$/.test(name)) continue;
    found.push(full);
  }
  return found;
};

describe("the wording of a refusal is the server's", () => {
  it("keeps the general access refusal in the interceptor alone", () => {
    const files = sources(join(CLIENT_ROOT, "src"));
    // A walk that finds nothing passes the assertion under it.
    expect(files.length).toBeGreaterThan(100);

    const carriers = files
      .filter((file) => readFileSync(file, "utf8").includes(ACCESS_REFUSAL))
      .map((file) => relative(REPO_ROOT, file).split("\\").join("/"))
      .sort();

    expect(carriers).toEqual([INTERCEPTOR]);
  });
});
