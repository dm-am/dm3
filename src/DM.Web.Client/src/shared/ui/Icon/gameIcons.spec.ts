/**
 * @vitest-environment node
 */

// The sprite and the server's list of icon names are one set: the server
// refuses a name the sprite has not got, and a name only the server knows
// draws a broken icon.
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";
import { GAME_ICONS } from "./gameIcons";

const CATALOG = join(
  CLIENT_SRC,
  "..",
  "..",
  "DM.Domain.Community",
  "Features",
  "Icons",
  "GameIconCatalog.cs",
);

/** The names the C# list writes, one quoted name to a line. */
const serverNames = (): string[] =>
  [...readFileSync(CATALOG, "utf8").matchAll(/^\s*"([a-z0-9-]+)",/gm)].map(
    ([, name]) => name,
  );

describe("the game icons", () => {
  it("are the names GameIconCatalog.cs accepts, no more and no fewer", () => {
    const server = serverNames();

    // A pattern that reads nothing would make the equality below vacuous.
    expect(server.length).toBeGreaterThan(10);
    expect(Object.keys(GAME_ICONS).sort()).toEqual([...server].sort());
  });
});
