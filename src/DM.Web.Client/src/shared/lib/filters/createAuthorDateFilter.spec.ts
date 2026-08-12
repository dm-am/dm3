/**
 * @vitest-environment node
 */

/**
 * The scaffold of a URL-synced author/date filter has one home.
 *
 * useTopicsFilter and useCommentsFilter were byte-for-byte twins of 280 lines:
 * the same eight actions, the same reducer, the same query parser and builder,
 * differing in their default sort and in the page-size preference they read. A
 * change to the common half had to be made twice, and nothing pointed at the
 * second place.
 *
 * The check is over the sources rather than over a mounted filter, because what
 * it guards is the shape of the file the next list will be copied from: a
 * module that declares the whole action set again is that copy starting. Lists
 * with a shape of their own (the pulse filter and its rating range, the games
 * filter and its hosts) declare a different set and are none of this rule.
 */
import { describe, it, expect } from "vitest";
import { existsSync, readdirSync, readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
// shared/lib/filters -> src
const FEATURES = join(resolve(HERE, "../../.."), "features");

/** The action set the shared filter owns, in full. */
const SHARED_ACTIONS = [
  "SET_SEARCH",
  "ADD_AUTHOR",
  "REMOVE_AUTHOR",
  "CLEAR_AUTHORS",
  "SET_DATE_RANGE",
  "SET_SORT",
  "TOGGLE_SORT_ORDER",
  "CLEAR_FILTERS",
];

/** Every model source of every filter feature. */
function filterModules(): { name: string; source: string }[] {
  const modules: { name: string; source: string }[] = [];
  for (const feature of readdirSync(FEATURES)) {
    const dir = join(FEATURES, feature, "model");
    if (!existsSync(dir)) continue;
    for (const file of readdirSync(dir)) {
      if (!file.endsWith(".ts") || file.endsWith(".spec.ts")) continue;
      modules.push({
        name: `${feature}/model/${file}`,
        source: readFileSync(join(dir, file), "utf8"),
      });
    }
  }
  return modules;
}

describe("the author/date filter scaffold", () => {
  const modules = filterModules();

  it("has filter modules to read at all", () => {
    // Without this the two checks below would pass by seeing nothing.
    expect(modules.length).toBeGreaterThan(0);
  });

  it("is what every list of that shape is built from", () => {
    const offenders = modules
      .filter(({ source }) =>
        SHARED_ACTIONS.every((action) => source.includes(`"${action}"`)),
      )
      .filter(({ source }) => !source.includes("createAuthorDateFilter"))
      .map(({ name }) => name);

    expect(
      offenders,
      "a second copy of the shared reducer and query parser: declare the sort options and the defaults and call createAuthorDateFilter",
    ).toEqual([]);
  });

  it("is used by more than one list", () => {
    // The check above passes for a module that declares no actions at all, so
    // the factory has to be seen in use, or the rule is measuring silence.
    const built = modules
      .filter(({ source }) => source.includes("createAuthorDateFilter"))
      .map(({ name }) => name);

    expect(built.length).toBeGreaterThan(1);
  });
});
