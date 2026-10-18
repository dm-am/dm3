/**
 * @vitest-environment node
 */

/**
 * The players counter of a game's status, "[0/∞]", is one token and does not
 * break inside its brackets.
 *
 * The status cell of a games table reads "Идет игра [0/∞]". In the profile's
 * games table at 1366 the column is narrow enough to wrap it, and the break
 * fell inside the counter: "Идет игра [0/" on one line and "∞]" on the next.
 * The break belongs between the words of the status, and the counter goes to
 * the next line whole. Both tables that print the counter are read here.
 */
import { describe, it, expect } from "vitest";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";

const HERE = dirname(fileURLToPath(import.meta.url));

const TABLES = [
  "pages/profile/ProfileGamesTable.vue",
  "widgets/games-table/GamesDataTable.vue",
];

describe("the players counter of a game's status", () => {
  it.each(TABLES)("stays on one line in %s", (file) => {
    const counter = styleRules(componentCss(join(HERE, file))).filter(
      (rule) => rule.selector === ".slots-indicator",
    );
    expect(counter.length).toBeGreaterThan(0);
    expect(counter.map((rule) => rule.declarations["white-space"])).toContain(
      "nowrap",
    );
  });
});
