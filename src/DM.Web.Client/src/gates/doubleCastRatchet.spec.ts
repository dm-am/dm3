/**
 * @vitest-environment node
 */

/**
 * The casts through `unknown` in the specs and their harness may only go down.
 * Site code has none: the dm/shipped block of eslint.config.js refuses a cast
 * of a cast there. The specs, fixtures, doubles and src/shared/lib/testing are
 * outside that block because a stand-in is partial on purpose, so here the
 * count has a ceiling instead of a ban.
 *
 * Each such cast is a value the compiler was told to stop checking: a stand-in
 * that drifts from the model it stands for passes until the screen breaks. The
 * cure is a builder of the model (the slice's fixtures file) that returns the
 * full type. The ceiling is the measured count, and a count below it fails too,
 * with the number to lower it to, so a removed cast cannot quietly come back.
 * Raising it widens what is allowed, which takes the owner's word.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join, relative, resolve } from "path";

import { CLIENT_SRC, collect } from "@/shared/lib/testing/sourceTree";

const CLIENT_DIR = resolve(CLIENT_SRC, "..");

const CEILING = 159;

/** The files dm/shipped leaves out: specs and their harness. */
const HARNESS_NAME = /\.(spec|fixtures|doubles)\.ts$/;
const TESTING_KIT = "src/shared/lib/testing/";

// Spelled in pieces so that this file does not count itself.
const DOUBLE_CAST = new RegExp(["\\bas", "unknown", "as\\b"].join("\\s+"), "g");

const asPath = (file: string) =>
  relative(CLIENT_DIR, file).split("\\").join("/");

function harnessFiles(): string[] {
  return ["src", "e2e"]
    .flatMap((dir) =>
      collect(join(CLIENT_DIR, dir), (name) => /\.(ts|vue)$/.test(name)),
    )
    .filter(
      (file) => HARNESS_NAME.test(file) || asPath(file).startsWith(TESTING_KIT),
    );
}

describe("double cast ratchet", () => {
  it("keeps the casts through unknown in specs and their harness at the ceiling", () => {
    const perFile = harnessFiles()
      .map((file) => ({
        path: asPath(file),
        count: readFileSync(file, "utf8").match(DOUBLE_CAST)?.length ?? 0,
      }))
      .filter(({ count }) => count > 0)
      .sort((a, b) => b.count - a.count);
    const total = perFile.reduce((sum, { count }) => sum + count, 0);
    const most = perFile
      .slice(0, 5)
      .map(({ path, count }) => `${path} ${count}`)
      .join(", ");

    expect(
      total,
      total > CEILING
        ? `${total} casts through unknown, ceiling ${CEILING}: build the value with the slice's fixtures instead of casting it. Most: ${most}`
        : `${total} casts through unknown, ceiling ${CEILING}: lower the ceiling to ${total}`,
    ).toBe(CEILING);
  });
});
