/**
 * A list with columns is a table, as DataTable: the table sizes its columns by
 * what they hold and is never narrower than its longest words, so in a narrow
 * column the list scrolls in its own frame instead of standing a letter per line.
 * Nothing in it measures: a column with a width keeps it, a fitting one takes
 * its longest line, the rest share what is left, one whole, several equally.
 */
import { describe, expect, it } from "vitest";
import { h } from "vue";
import { mount } from "@vue/test-utils";
import { join } from "path";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";
import ExpandableList, {
  type ExpandableListColumn,
} from "./ExpandableList.vue";

const items = [
  { id: "a", subject: "Короткая тема", type: "Ошибка", date: "28.09.2026" },
  { id: "b", subject: "Длиннейшее слово", type: "Ошибка", date: "28.09.2026" },
];
const columns = [
  { key: "subject" as const, label: "Тема" },
  { key: "type" as const, label: "Тип", width: "180px" },
  { key: "date" as const, label: "Дата", width: "130px" },
];

const widthsOf = (wrapper: ReturnType<typeof mount>, cell: string) =>
  wrapper.findAll(cell).map((one) => (one.element as HTMLElement).style.width);

describe("ExpandableList with columns", () => {
  it("is one table that sizes its columns itself", () => {
    const wrapper = mount(ExpandableList, { props: { items, columns } });
    const list = wrapper.get(".expandable-list");

    expect(list.classes()).toContain("expandable-list--columns");
    // The scrolling frame holds the table alone, as DataTable's does.
    expect(list.element.children).toHaveLength(1);
    expect(list.element.firstElementChild!.tagName).toBe("TABLE");
    expect(wrapper.findAll("th.header-cell").map((th) => th.text())).toEqual([
      "Тема",
      "Тип",
      "Дата",
    ]);
    expect(wrapper.findAll("tbody > tr.expandable-row--grid")).toHaveLength(2);
    // No measuring and no width of its own: the px columns keep theirs.
    expect(list.attributes("style")).toBeUndefined();
    expect(widthsOf(wrapper, "th.header-cell")).toEqual(["", "180px", "130px"]);
    wrapper.unmount();
  });

  it("lays the table out by its content, a fitting column at its longest line", () => {
    const css = ruleMap(
      componentCss(
        join(CLIENT_SRC, "shared/ui/ExpandableList/ExpandableList.vue"),
      ),
    );

    expect(css.get(".expandable-table")).toMatchObject({
      width: "100%",
      "table-layout": "auto",
    });
    expect(css.get(".cell--fit")).toEqual({
      width: "1px",
      "white-space": "nowrap",
    });
  });

  it("opens a panel across the whole row, the marker's cell included", async () => {
    const wrapper = mount(ExpandableList, { props: { items, columns } });

    const cells = wrapper.findAll("td.details-cell");
    expect(cells.map((td) => td.attributes("colspan"))).toEqual(["4", "4"]);
    await wrapper.findAll(".expandable-row--grid")[1]!.trigger("click");
    expect(wrapper.findAll(".details-grid")[1]!.classes()).toContain("open");
    wrapper.unmount();
  });

  it("asks for no table without columns", () => {
    const wrapper = mount(ExpandableList, {
      props: { items: [{ id: "a", title: "Раздел" }] },
    });

    expect(wrapper.find("table").exists()).toBe(false);
    expect(wrapper.get(".expandable-list").classes()).not.toContain(
      "expandable-list--columns",
    );
  });
});

/**
 * A list of plain rows: the staff table of the rules page. Every column stands
 * in the row at every width of the list, as on the full version, and a row
 * opens only where it has details of its own, by its marker.
 */
describe("ExpandableList of plain rows", () => {
  const staff = [
    {
      id: "Admin",
      title: "Администраторы",
      description: "Определяют политику сайта",
      users: "SolohinLex",
    },
    {
      id: "Moderator",
      title: "Модераторы",
      description: "Следят за порядком",
      users: "Многоуважаемый_Шкаф1",
    },
  ];
  const staffColumns: ExpandableListColumn[] = [
    { key: "title", label: "Роль", fit: true },
    { key: "description", label: "Функции" },
    { key: "users", label: "Состав", fit: true },
  ];

  function mountStaff(slots = {}, shown = staffColumns) {
    return mount(ExpandableList, {
      props: {
        items: staff,
        columns: shown,
        allowMultiple: true,
        plainRows: true,
      },
      slots,
    });
  }

  it("keeps every column in the row, and a row with nothing to open is a plain row", async () => {
    const wrapper = mountStaff();

    const rows = wrapper.findAll(".expandable-row--grid");
    for (const row of rows) {
      expect(row.attributes("role")).toBeUndefined();
      expect(row.attributes("tabindex")).toBeUndefined();
      expect(row.classes()).toContain("expandable-row--static");
      expect(row.findAll(".row-cell")).toHaveLength(3);
    }
    // No marker promises a panel that would open empty.
    expect(wrapper.findAll(".icon-cell")).toHaveLength(0);
    expect(wrapper.findAll(".expand-toggle")).toHaveLength(0);
    expect(
      wrapper.findAll("td.details-cell").map((td) => td.attributes("colspan")),
    ).toEqual(["3", "3"]);

    await rows[0]!.trigger("click");
    expect(wrapper.findAll(".details-grid.open")).toHaveLength(0);
    wrapper.unmount();
  });

  it("opens a row with details of its own by its marker, and by a press anywhere in the row but on a link", async () => {
    const wrapper = mountStaff({
      "cell-title": (props: { item: (typeof staff)[number] }) =>
        h("a", { href: "#role", class: "role-link" }, props.item.title),
      "item-Admin": () => h("p", "Подробнее о роли"),
    });
    const [firstRow, secondRow] = wrapper.findAll(".expandable-row--grid");
    const toggle = firstRow!.get("button.expand-toggle");
    expect(toggle.attributes("aria-controls")).toBe("expandable-details-Admin");
    const nameId = toggle.attributes("aria-labelledby")!;
    expect(wrapper.get(`#${nameId}`).text()).toBe("Администраторы");
    expect(secondRow!.find("button.expand-toggle").exists()).toBe(false);

    await firstRow!.get(".role-link").trigger("click");
    await firstRow!.get(".role-link").trigger("keydown", { key: "Enter" });
    expect(toggle.attributes("aria-expanded")).toBe("false");

    await firstRow!.findAll(".row-cell")[1]!.trigger("click");
    expect(toggle.attributes("aria-expanded")).toBe("true");

    // The marker toggles once, not once by itself and once by the row.
    await toggle.trigger("click");
    expect(toggle.attributes("aria-expanded")).toBe("false");
    wrapper.unmount();
  });

  it("gives the fitting columns their longest line and the rest to the others, equally", () => {
    const one = mountStaff();
    // One column shares: the table gives it all that is left, no width needed.
    expect(widthsOf(one, "th.header-cell")).toEqual(["", "", ""]);
    expect(
      one.findAll("th.header-cell").map((th) => th.classes("cell--fit")),
    ).toEqual([true, false, true]);
    // Every cell of a fitting column, not its heading alone: the longest line may be a row's.
    expect(
      one
        .findAll("tr.expandable-row--grid")[0]!
        .findAll("td.row-cell")
        .map((td) => td.classes("cell--fit")),
    ).toEqual([true, false, true]);
    one.unmount();

    const two = mountStaff({}, [
      ...staffColumns.slice(0, 2),
      { key: "users", label: "Состав" },
    ]);
    expect(widthsOf(two, "th.header-cell")).toEqual(["", "50%", "50%"]);
    expect(widthsOf(two, "td.row-cell").slice(0, 3)).toEqual([
      "",
      "50%",
      "50%",
    ]);
    two.unmount();
  });
});

describe("ExpandableList that does not fold", () => {
  it("keeps every row a toggle", async () => {
    const wrapper = mount(ExpandableList, { props: { items, columns } });

    for (const row of wrapper.findAll(".expandable-row--grid")) {
      expect(row.attributes("role")).toBe("button");
      expect(row.attributes("tabindex")).toBe("0");
      expect(row.attributes("aria-expanded")).toBe("false");
      expect(row.get(".expand-marker").attributes("aria-hidden")).toBe("true");
    }
    expect(wrapper.findAll(".expand-toggle")).toHaveLength(0);
    // One flexible column: the table layout gives it the rest by itself.
    expect((wrapper.get(".row-cell").element as HTMLElement).style.width).toBe(
      "",
    );

    await wrapper.findAll(".expandable-row--grid")[1]!.trigger("click");
    expect(wrapper.findAll(".details-grid")[1]!.classes()).toContain("open");
    wrapper.unmount();
  });
});
