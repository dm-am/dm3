/**
 * @vitest-environment jsdom
 */

/**
 * The site respects "reduce motion" everywhere a media query can reach, and the
 * one place it could not reach was the only JavaScript animation in the tree:
 * the FLIP flight that plays when the profile tabs and the forum board strip
 * reorder themselves. 280ms of movement, at every switch, for exactly the
 * readers the setting exists to protect — and the discrepancy was invisible,
 * because everything ELSE in the project obeys.
 *
 * Two checks, because the defect has two halves. The helper has to read the
 * setting correctly, and the animation has to consult the helper. The second is
 * a fact about the sources, so it is read from them: a new `.animate(` call
 * added elsewhere would be the same defect again.
 */
import { describe, it, expect, afterEach, vi } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";
import { prefersReducedMotion } from "./motion";

const HERE = dirname(fileURLToPath(import.meta.url));
// utils -> lib -> shared -> src
const CLIENT_SRC = resolve(HERE, "..", "..", "..");

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

function collect(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (
      (full.endsWith(".ts") || full.endsWith(".vue")) &&
      !full.endsWith(".spec.ts")
    ) {
      out.push(full);
    }
  }
  return out;
}

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

/** Replaces window.matchMedia with one that answers the given verdict. */
function answerReducedMotion(reduce: boolean) {
  vi.stubGlobal("matchMedia", (query: string) => ({
    matches: query.includes("prefers-reduced-motion: reduce") && reduce,
    media: query,
  }));
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("prefersReducedMotion", () => {
  it("answers the query the operating system is asked", () => {
    answerReducedMotion(true);
    expect(prefersReducedMotion()).toBe(true);
  });

  it("says no when the setting is off", () => {
    answerReducedMotion(false);
    expect(prefersReducedMotion()).toBe(false);
  });

  it("says no where there is no window to ask", () => {
    // Server-side, or a bare node test environment: no setting to honour, and
    // certainly no animation running.
    vi.stubGlobal("matchMedia", undefined);
    expect(prefersReducedMotion()).toBe(false);
  });
});

describe("a JavaScript animation", () => {
  it("is started only by code that consults the setting", () => {
    const offenders: string[] = [];
    for (const file of collect(CLIENT_SRC)) {
      const source = readFileSync(file, "utf8");
      if (!/\.animate\(/.test(source)) continue;
      if (source.includes("prefersReducedMotion")) continue;
      offenders.push(
        `${asPath(file)}: starts a Web Animations animation, which no media query can switch off, without asking prefersReducedMotion()`,
      );
    }
    expect(offenders).toEqual([]);
  });

  it("is still found by this walk", () => {
    // An empty result above would pass by scanning nothing. There is exactly
    // one such caller, and this names it.
    const callers = collect(CLIENT_SRC)
      .filter((file) => /\.animate\(/.test(readFileSync(file, "utf8")))
      .map(asPath);
    expect(callers).toContain("shared/lib/composables/useFlipReorder.ts");
  });
});
