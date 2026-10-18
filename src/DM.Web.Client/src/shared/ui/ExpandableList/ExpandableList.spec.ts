/**
 * A list with columns is never narrower than its columns: the ones with a
 * width in px at that width, the one without it at its longest word, which
 * the list measures in a hidden copy of each of its cells. On a phone the
 * columns with a width used to take the whole row and the subject stood one
 * letter per line.
 */
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";
import { h, nextTick } from "vue";
import { mount } from "@vue/test-utils";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import ExpandableList from "./ExpandableList.vue";

const items = [
  { id: "a", subject: "Короткая тема", type: "Ошибка", date: "28.09.2026" },
  { id: "b", subject: "Длиннейшее слово", type: "Ошибка", date: "28.09.2026" },
];
const columns = [
  { key: "subject" as const, label: "Тема" },
  { key: "type" as const, label: "Тип", width: "180px" },
  { key: "date" as const, label: "Дата", width: "130px" },
];

// jsdom lays nothing out and drops min-content from a style: the hidden copy
// of a cell answers with the width of its longest word, ten per letter, as a
// browser lays out the copy the stylesheet sizes at min-content.
function measureLongestWord() {
  return vi
    .spyOn(HTMLElement.prototype, "getBoundingClientRect")
    .mockImplementation(function (this: HTMLElement) {
      if (!this.classList.contains("measure-copy"))
        return { width: 0 } as DOMRect;
      const words = this.textContent!.trim().split(/\s+/);
      return { width: Math.max(...words.map((w) => w.length)) * 10 } as DOMRect;
    });
}

function minWidthOf(wrapper: ReturnType<typeof mount>) {
  return (
    wrapper.get(".expandable-list").element as HTMLElement
  ).style.getPropertyValue("--expandable-min-width");
}

afterEach(() => vi.restoreAllMocks());

describe("ExpandableList with columns", () => {
  it("is as wide as its columns, the one without a width at its longest word", async () => {
    measureLongestWord();
    const wrapper = mount(ExpandableList, {
      props: { items, columns },
      attachTo: document.body,
    });
    await wrapper.vm.$nextTick();

    // marker 20 + row padding 24 + "Длиннейшее" 100 + (8 + 180) + (8 + 130)
    expect(minWidthOf(wrapper)).toBe("470px");
    expect(wrapper.get(".expandable-list").classes()).toContain(
      "expandable-list--columns",
    );
    wrapper.unmount();
  });

  it("measures again when the rows change", async () => {
    measureLongestWord();
    const wrapper = mount(ExpandableList, {
      props: { items, columns },
      attachTo: document.body,
    });
    await wrapper.vm.$nextTick();

    await wrapper.setProps({
      items: [{ ...items[0]!, id: "c", subject: "Наидлиннейшеслово" }],
    });
    await wrapper.vm.$nextTick();

    // "Наидлиннейшеслово" 170 in place of 100
    expect(minWidthOf(wrapper)).toBe("540px");
    wrapper.unmount();
  });

  it("leaves no hidden copy behind", async () => {
    measureLongestWord();
    const wrapper = mount(ExpandableList, {
      props: { items, columns },
      attachTo: document.body,
    });
    await wrapper.vm.$nextTick();

    expect(wrapper.findAll(".measure-copy")).toHaveLength(0);
    expect(wrapper.findAll(".row-cell")).toHaveLength(6);
    wrapper.unmount();
  });

  it("lays the copy out at its min-content width", () => {
    const source = readFileSync(
      resolve(__dirname, "ExpandableList.vue"),
      "utf8",
    );
    const rule = source.match(/^\.measure-copy\n((?: {2}.+\n)+)/m)?.[1] ?? "";

    expect(rule).toContain("position: absolute");
    expect(rule).toContain("width: min-content");
  });

  it("asks for no width without columns", () => {
    const wrapper = mount(ExpandableList, {
      props: { items: [{ id: "a", title: "Раздел" }] },
    });

    expect(minWidthOf(wrapper)).toBe("");
    expect(wrapper.get(".expandable-list").classes()).not.toContain(
      "expandable-list--columns",
    );
  });
});

/**
 * A list of plain rows: the staff table of the rules page. Every column stands
 * in the row at every width of the list, as on the full version, and a row
 * opens only where it has details of its own, by its marker. Where the list is
 * narrower than its columns need, it scrolls in its own box, the last resort.
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
  const staffColumns = [
    { key: "title" as const, label: "Роль" },
    { key: "description" as const, label: "Функции" },
    { key: "users" as const, label: "Состав" },
  ];

  /** The list's one observer, called by hand: jsdom lays nothing out. */
  class FakeObserver {
    static last: FakeObserver | null = null;
    constructor(readonly callback: () => void) {
      FakeObserver.last = this;
    }
    readonly observed = new Set<Element>();
    observe(element: Element) {
      this.observed.add(element);
    }
    unobserve() {}
    disconnect() {}
  }

  // The width of the list, which the list reads from its own box. A copy of a
  // cell answers like a browser: the width the list gave the cell when it has
  // one inline, else its longest word at ten per letter and the 8px gap in
  // front of it.
  function layOut(initial: number) {
    let width = initial;
    vi.stubGlobal("ResizeObserver", FakeObserver);
    vi.spyOn(HTMLElement.prototype, "offsetWidth", "get").mockImplementation(
      function (this: HTMLElement) {
        return this.classList.contains("expandable-list") ? width : 0;
      },
    );
    vi.spyOn(Element.prototype, "clientWidth", "get").mockImplementation(
      function (this: Element) {
        return this.classList.contains("expandable-list") ? width - 2 : 0;
      },
    );
    vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(
      function (this: HTMLElement) {
        if (!this.classList.contains("measure-copy"))
          return { width: 0 } as DOMRect;
        if (this.style.width)
          return { width: parseFloat(this.style.width) } as DOMRect;
        const words = this.textContent!.trim().split(/\s+/);
        return {
          width: Math.max(...words.map((w) => w.length)) * 10 + 8,
        } as DOMRect;
      },
    );
    return async (next: number) => {
      width = next;
      // Only what the list asked to watch reports a new size.
      if (FakeObserver.last?.observed.size) FakeObserver.last.callback();
      await nextTick();
      await nextTick();
    };
  }

  async function mountStaff(width: number, slots = {}) {
    const resize = layOut(width);
    const wrapper = mount(ExpandableList, {
      props: {
        items: staff,
        columns: staffColumns,
        allowMultiple: true,
        plainRows: true,
      },
      slots,
      attachTo: document.body,
    });
    await nextTick();
    await nextTick();
    return { wrapper, resize };
  }

  const labels = (wrapper: ReturnType<typeof mount>) =>
    wrapper.findAll(".header-cell").map((cell) => cell.text());

  const widthsOf = (wrapper: ReturnType<typeof mount>) =>
    wrapper
      .findAll(".header-cell")
      .map((cell) => (cell.element as HTMLElement).style.width);

  afterEach(() => {
    vi.unstubAllGlobals();
    FakeObserver.last = null;
    document.body.innerHTML = "";
  });

  it("keeps every column in the row at any width, and a row with nothing to open is a plain row", async () => {
    const { wrapper, resize } = await mountStaff(600);

    for (const width of [600, 300, 600]) {
      await resize(width);
      expect(labels(wrapper), `${width}`).toEqual([
        "Роль",
        "Функции",
        "Состав",
      ]);
      expect(wrapper.findAll(".folded-cell"), `${width}`).toHaveLength(0);
    }
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

    await rows[0]!.trigger("click");
    expect(wrapper.findAll(".details-grid.open")).toHaveLength(0);
    wrapper.unmount();
  });

  it("opens a row with details of its own by its marker, and by a press anywhere in the row but on a link", async () => {
    const { wrapper } = await mountStaff(600, {
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

  it("gives every column its longest word and shares the rest, and scrolls where its words do not fit", async () => {
    const { wrapper, resize } = await mountStaff(600);

    // 598 inside the frame, less the row's padding: 574. The words take 148
    // ("Администраторы"), 108 ("Определяют") and 208 ("Многоуважаемый_Шкаф1"),
    // and each column gets a third of the 110 over them; the last takes the
    // rest, and every cell after the first keeps its gap of 8.
    expect(widthsOf(wrapper)).toEqual(["184px", "136px", ""]);
    // The row's padding and the three words.
    expect(minWidthOf(wrapper)).toBe("488px");

    // Narrower than its words: every column keeps its word, and the list is
    // as wide as they are, in a box that scrolls.
    await resize(300);
    expect(widthsOf(wrapper)).toEqual(["148px", "100px", ""]);
    expect(minWidthOf(wrapper)).toBe("488px");
    wrapper.unmount();
  });
});

describe("ExpandableList cuts nothing", () => {
  it("ends no line in an ellipsis, and keeps no form of the list for a narrow window", () => {
    const css = componentCss(resolve(__dirname, "ExpandableList.vue"));
    expect(css).not.toMatch(/text-overflow|line-clamp/);
    expect(css).not.toMatch(/@media/);
    expect(ruleMap(css).has(".row-cell--yields")).toBe(false);
  });
});

describe("ExpandableList that does not fold", () => {
  it("keeps every row a toggle and takes no width of its own", async () => {
    measureLongestWord();
    const wrapper = mount(ExpandableList, {
      props: { items, columns },
      attachTo: document.body,
    });
    await nextTick();

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
