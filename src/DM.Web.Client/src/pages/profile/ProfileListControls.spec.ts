/**
 * The rows over the lists of a profile, games and blogs: the search, the sort
 * and, over the games, the toggle "Ведущий / Игрок". As before 5 October
 * 2026: the field "Поиск по названию" takes the line, down to 200, and what
 * does not fit beside it goes to the next line whole, the sort with its words
 * and the toggle with both of its segments. Nothing scrolls sideways and
 * nothing gives up its words (owner, 12 October 2026): the wave of 5 October
 * kept the row one line by cutting the hint of the field to "Поиск" and the
 * sort to its icon.
 *
 * Read off the stylesheets, which a document that lays nothing out cannot
 * show; that the rows stand as described is the live stand's to show.
 */
import { describe, it, expect } from "vitest";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { readFileSync } from "node:fs";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";

const here = dirname(fileURLToPath(import.meta.url));

describe.each([["ProfileGamesTable.vue"], ["ProfileBlogsTable.vue"]])(
  "the row over the list of %s",
  (file) => {
    const path = join(here, file);
    const source = readFileSync(path, "utf8");
    const css = componentCss(path);
    const rules = styleRules(css);
    const of = (selector: string) =>
      rules
        .filter((rule) => rule.selector === selector)
        .map(({ declarations }) => declarations);

    it("puts what does not fit beside the field on the next line, and never scrolls", () => {
      expect(of(".controls")[0]?.["flex-wrap"]).toBe("wrap");
      expect(source).not.toMatch(/ScrollRow|scroll-row/);
    });

    it("gives the field the line down to 200, with its whole hint", () => {
      expect(of(".search")[0]).toMatchObject({
        flex: "1",
        "min-width": "200px",
      });
      expect(source).toContain('placeholder="Поиск по названию"');
      expect(source).not.toMatch(/useSearchHint|searchHint|search--bare/);
    });

    it("leaves the sort its words at every width of the row", () => {
      expect(css).not.toMatch(/@container|@media/);
      expect(css).not.toContain("sort-btn");
    });
  },
);

describe("the toggle over the games of a profile", () => {
  const path = join(here, "ProfileGamesTable.vue");
  const css = componentCss(path);

  it("goes under the field with the sort, as one", () => {
    const pair = readFileSync(path, "utf8").match(
      /<div class="controls-pair">([\s\S]*?)<\/div>/,
    );
    expect(pair?.[1]).toMatch(/<SortButton[\s\S]*<SegmentedControl/);
    expect(
      styleRules(css).find((rule) => rule.selector === ".controls-pair")
        ?.declarations,
    ).toMatchObject({ display: "flex", "flex-wrap": "wrap" });
  });

  it("stands whole, its segments as everywhere", () => {
    const rules = styleRules(css);
    expect(
      rules.find((rule) => rule.selector === ".role-toggle")?.declarations.flex,
    ).toBe("none");
    expect(
      rules.find((rule) => rule.selector === ".role-toggle :deep(.segment)"),
    ).toBeUndefined();
  });
});

/**
 * The bar of the edit mode of a profile says its state in words at every
 * width, beside "Сохранить" and "Отмена" ("Завершить" while nothing has
 * changed), as before 5 October 2026: the words, then the two buttons at the
 * right, in one line, as at 1366 and on a phone. The wave of 5 October hid
 * the words where the page is narrow. Where the column is narrower than the
 * words and the buttons together (the middle column of a window of 1024), the
 * two buttons go down a line together, still at the right, and nothing stands
 * past the edge of the bar.
 */
describe("the bar of the edit mode", () => {
  const path = join(here, "ProfilePage.vue");
  const source = readFileSync(path, "utf8");
  const css = componentCss(path);
  const rules = styleRules(css);
  const of = (selector: string) =>
    rules
      .filter((rule) => rule.selector === selector)
      .map(({ declarations }) => declarations);

  it("says its words at every width and breaks them between words only", () => {
    expect(css).not.toMatch(/@container/);
    expect(of(".profile-page")[0]?.container).toBeUndefined();
    const status = of(".save-bar-status");
    expect(status).toHaveLength(1);
    expect(status[0].flex).toBe("1");
    for (const property of ["white-space", "min-width", "visibility"])
      expect(status[0][property], property).toBeUndefined();
  });

  it("takes the two buttons down a line together where the words and they do not fit", () => {
    expect(of(".save-bar")[0]?.["flex-wrap"]).toBe("wrap");
    expect(of(".save-bar-actions")[0]).toMatchObject({
      display: "flex",
      "margin-left": "auto",
    });
    expect(source).toMatch(
      /class="save-bar-actions"[^]*?Сохранить[^]*?Завершить[^]*?<\/div>/,
    );
  });
});
