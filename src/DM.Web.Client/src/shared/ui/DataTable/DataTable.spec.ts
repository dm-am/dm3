/**
 * @vitest-environment jsdom
 */

/**
 * The grid of a DataTable, as far as the markup carries it: every column the
 * caller declares is drawn, in the header and in every row, at the width it
 * declares, at any width of the window. There is no second form of the table
 * in which a column moves under another, takes another width or goes. How the
 * text of a cell breaks is the stylesheet's (the last block); jsdom lays
 * nothing out, so the e2e spec tables-fit.spec.ts measures it.
 *
 * And the line a caller opens under one row (`detailsFor`): a refusal about
 * that row, across the whole grid.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { defineComponent, h, type Component } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, ruleMap, styleRules } from "@/shared/lib/testing/styles";
import DataTable from "./DataTable.vue";
import type { Column } from "./types";

interface Row {
  id: string;
  title: string;
  author: string;
  created: string;
  comments: number;
}

const ROWS: Row[] = [
  {
    id: "t-1",
    title: "Правила форума",
    author: "Player.Three",
    created: "05.09.2026 в 11:12",
    comments: 6,
  },
  {
    id: "t-2",
    title: "Добро пожаловать на форум!",
    author: "Player_One",
    created: "29.08.2026 в 02:09",
    comments: 5,
  },
];

const COLUMNS: Column[] = [
  { key: "title", label: "Топик", align: "left" },
  { key: "author", label: "Автор", width: "180px", align: "left" },
  { key: "created", label: "Дата создания", width: "134px", align: "center" },
  { key: "comments", label: "Комментарии", width: "10%", align: "center" },
];

/**
 * Handed to h() as a plain component. The generic one declares every slot as
 * required (defineSlots), and a table here fills only the slots it needs, as
 * the pages do through their templates.
 */
const Table = DataTable as unknown as Component;

function render(props: Record<string, unknown> = {}) {
  return mount(
    defineComponent({
      render: () =>
        h(
          Table,
          { columns: COLUMNS, data: ROWS, ...props },
          {
            "cell-author": ({ row }: { row: Row }) =>
              h("a", { class: "author" }, row.author),
            details: ({ row }: { row: Row }) =>
              h("p", { class: "refusal" }, `Не удалось: ${row.title}`),
          },
        ),
    }),
  );
}

describe("DataTable columns", () => {
  it("draws every declared column in the header and in every row", () => {
    const table = render();

    expect(table.findAll("thead th").map((cell) => cell.text())).toEqual(
      COLUMNS.map((column) => column.label),
    );
    for (const row of table.findAll("tr.table-row")) {
      expect(
        row
          .findAll("td")
          .map((cell) =>
            cell
              .classes()
              .find((one) => one.startsWith("col-") && one !== "col"),
          ),
      ).toEqual(COLUMNS.map((column) => `col-${column.key}`));
    }
    // A cell slot draws its value in the cell itself, and nowhere else.
    expect(table.findAll("a.author").map((link) => link.text())).toEqual([
      "Player.Three",
      "Player_One",
    ]);
  });

  it("gives each header the width its column declares, and no other", () => {
    const table = render();
    const header = (key: string) => table.get(`thead th.col-${key}`);

    expect(header("title").attributes("style")).toBeUndefined();
    expect(header("author").attributes("style")).toBe("width: 180px;");
    expect(header("created").attributes("style")).toBe("width: 134px;");
    expect(header("comments").attributes("style")).toBe("width: 10%;");
  });
});

describe("DataTable row class", () => {
  // A row the caller sets apart (a hidden contest series, dimmed) carries the
  // class the caller names for it, and the others none.
  it("marks the rows its caller names, and only them", () => {
    const table = render({
      rowClass: (row: Row) => (row.comments > 5 ? "is-busy" : undefined),
    });

    expect(
      table.findAll("tr.table-row").map((row) => row.classes("is-busy")),
    ).toEqual([true, false]);
  });
});

describe("DataTable line under a row", () => {
  it("opens under the row it names, across every column", () => {
    const table = render({ detailsFor: "t-2", showRowNumbers: true });

    const rows = table.findAll("tbody tr");
    expect(rows.map((row) => row.classes()[0])).toEqual([
      "table-row",
      "table-row",
      "table-details-row",
    ]);
    const line = rows[2].get("td").element as HTMLTableCellElement;
    // The row number is a column of the grid too.
    expect(line.colSpan).toBe(COLUMNS.length + 1);
    expect(rows[2].get("p.refusal").text()).toBe(
      "Не удалось: Добро пожаловать на форум!",
    );
  });

  it("is not there while no row is named", () => {
    const table = render();

    expect(table.find("tr.table-details-row").exists()).toBe(false);
    expect(table.find("p.refusal").exists()).toBe(false);
  });
});

describe("DataTable box", () => {
  // The table scrolls sideways in a box of its own only where the words of
  // its columns are wider than the column, and the page does not (e2e
  // tables-fit).
  it("stands the table in a box of its own, the one that scrolls", () => {
    const table = render();

    expect(table.find(".data-table-scroll > table.data-table").exists()).toBe(
      true,
    );
  });
});

/**
 * The footer, the paging of the list, under the part of the table the reader
 * sees: a table wider than its column scrolls in its box, and a footer as wide
 * as the table centred the strip under all of it, so on a phone ">>" stood out
 * of sight and the strip measured a line it did not have (Paging.vue).
 */
describe("DataTable footer", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("tells its line the width of the box the table scrolls in", async () => {
    let resized: ((entries: unknown[]) => void) | undefined;
    vi.stubGlobal(
      "ResizeObserver",
      vi.fn(function (callback: (entries: unknown[]) => void) {
        resized = callback;
        return { observe: vi.fn(), unobserve: vi.fn(), disconnect: vi.fn() };
      }),
    );
    const table = mount(
      defineComponent({
        render: () =>
          h(
            Table,
            { columns: COLUMNS, data: ROWS },
            { footer: () => h("nav", { class: "paging" }, "1 2 3") },
          ),
      }),
    );
    resized?.([{ contentRect: { width: 296 } }]);
    await flushPromises();

    const box = table.get(".data-table-scroll");
    expect(box.attributes("style")).toContain("--data-table-box: 296px");
    expect(
      table.find("tfoot td > .table-footer-line > nav.paging").exists(),
    ).toBe(true);
  });

  it("holds the line at the left of the box, as wide as the box less the cell", () => {
    const rules = ruleMap(
      componentCss(
        join(dirname(fileURLToPath(import.meta.url)), "DataTable.vue"),
      ),
    );
    expect(rules.get(".table-footer-line")).toEqual({
      position: "sticky",
      left: "13px",
      width: "calc(var(--data-table-box) - 26px)",
    });
  });
});

/**
 * Every column stands at every width of the window: no column of a table goes,
 * and the stylesheet keeps no other form of the table for a narrow column or a
 * phone. A word of a cell is never broken inside: where the words of the
 * columns do not fit, the box scrolls (DataTable box). jsdom lays nothing out,
 * so the rules are read off the stylesheet.
 */
describe("DataTable at any width", () => {
  const css = componentCss(
    join(dirname(fileURLToPath(import.meta.url)), "DataTable.vue"),
  );
  const rules = styleRules(css);

  it("hides no cell and keeps no form of the table for a narrow column", () => {
    expect(css).not.toMatch(/@media|@container/);
    expect(css).not.toContain("col--hidden");
    expect(css).not.toContain("hide-mobile");
    const hidden = rules.filter(
      (rule) =>
        rule.declarations.display === "none" &&
        /\b(th|td)\b|col/.test(rule.selector),
    );
    expect(hidden).toEqual([]);
  });

  it("breaks the words of a cell and of a header between words only", () => {
    const wrapOf = (selector: string) =>
      rules
        .filter((rule) => rule.selector.split(/\s*,\s*/).includes(selector))
        .map((rule) => rule.declarations["overflow-wrap"])
        .filter(Boolean)
        .at(-1);

    expect(wrapOf(":where(.data-table) td")).toBe("normal");
    expect(wrapOf(":where(.data-table) th")).toBe("normal");
    expect(
      rules.find((rule) => rule.selector === ".data-table :deep(:is(th, td) *)")
        ?.declarations["overflow-wrap"],
    ).toBe("normal");
  });

  it("draws the header and every row with every column, the row numbers too", () => {
    const table = render({ showRowNumbers: true });

    expect(table.findAll("thead th").map((cell) => cell.text())).toEqual([
      "#",
      ...COLUMNS.map((column) => column.label),
    ]);
    for (const row of table.findAll("tr.table-row")) {
      expect(row.findAll("td")).toHaveLength(COLUMNS.length + 1);
    }
  });
});
