/**
 * @vitest-environment jsdom
 */

/**
 * "Reduce motion" for the one JavaScript animation, the FLIP flight of the
 * profile tabs and the board strip, which no media query reaches: the helper
 * reads the setting, and every `.animate(` call in the sources consults it.
 */
import { describe, it, expect, afterEach, vi } from "vitest";
import { readFileSync } from "fs";
import { prefersReducedMotion } from "./motion";
import {
  CLIENT_SRC,
  asPath,
  collect as collectAll,
} from "@/shared/lib/testing/sourceTree";

const collect = (dir: string): string[] =>
  collectAll(
    dir,
    (name) =>
      (name.endsWith(".ts") || name.endsWith(".vue")) &&
      !name.endsWith(".spec.ts"),
  );

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
