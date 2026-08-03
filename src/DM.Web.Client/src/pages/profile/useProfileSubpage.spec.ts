/**
 * @vitest-environment node
 */

/**
 * The scaffold of a profile subpage has one home.
 *
 * Five pages resolved the owner, decided the 404, built the profile link and
 * set the document title on their own, in the same twenty lines. Two of the
 * pairs differed by a single string, which is what made them the closest
 * duplicates in the tree: a change to the shared part had to be applied twice,
 * and nothing pointed at the second place.
 *
 * The check is over the sources rather than over a mounted page, because what
 * it guards is the shape of the file the next subpage will be copied from. A
 * page that reaches for useProfileSubpageUser directly is that copy starting.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));

const read = (name: string) => readFileSync(join(HERE, name), "utf8");

const pages = readdirSync(HERE).filter((name) => name.endsWith(".vue"));

describe("profile subpage scaffold", () => {
  it("has exactly one file resolving the subpage owner", () => {
    const importers = pages.filter((name) =>
      read(name).includes('from "./useProfileSubpageUser"'),
    );

    expect(importers).toEqual([]);
  });

  it("gives every subpage its title and its 404 from that one file", () => {
    const offenders = pages.filter((name) => {
      const source = read(name);
      return (
        source.includes("<ProfileSubpageHeader") &&
        !source.includes("useProfileSubpage(")
      );
    });

    expect(offenders).toEqual([]);
  });
});
