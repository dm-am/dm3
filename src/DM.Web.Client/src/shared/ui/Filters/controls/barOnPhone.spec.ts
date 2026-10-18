/**
 * @vitest-environment jsdom
 */

/**
 * The filter bar on a phone is the bar of 1366: the search, "Фильтры" and the
 * sort with their words, in one line. No width of the window or of the bar
 * changes how it stands: the buttons gave up their words on a phone once, and
 * once the search took a line of its own in a bar narrower than its words;
 * both were forms of the bar for a phone, and a phone shows the full version.
 * Only a column too narrow for the buttons beside the search (the middle
 * column of 1024) sends what does not fit to the next line, so the bar never
 * runs past it.
 */
import { describe, it, expect, afterEach } from "vitest";
import { defineComponent, h, nextTick, ref } from "vue";
import { mount, type VueWrapper } from "@vue/test-utils";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  compileSass,
  componentCss,
  ruleMap,
} from "@/shared/lib/testing/styles";
import FilterButton from "./FilterButton.vue";
import SortButton from "./SortButton.vue";
import { useKeepInWindow, WINDOW_EDGE } from "./useKeepInWindow";

const HERE = dirname(fileURLToPath(import.meta.url));

/** The bar of a list, as the filters of a list write it. */
const BAR = compileSass(
  '@use "@/assets/styles/Filters" as *\n.probe\n  +filter-bar\n  +filter-responsive',
).replace(/\s+/g, " ");

describe("the buttons of the bar", () => {
  it("keep their words and the sides of a page's button at every width", () => {
    for (const [file, button, label] of [
      ["FilterButton.vue", ".filter-btn", ".filter-btn-label"],
      ["SortButton.vue", ".sort-btn", ".sort-btn-label"],
    ]) {
      const css = componentCss(join(HERE, file)).replace(/\s+/g, " ");
      // No width of the window or of the bar changes how the button stands.
      expect(css, file).not.toMatch(/@container|@media/);
      const rules = ruleMap(css);
      expect(rules.get(label)?.display, file).toBeUndefined();
      expect(rules.get(button)?.width, file).toBeUndefined();
      expect(rules.get(button)?.padding, file).toBe("0 16px");
    }
  });

  it("are named by their words, which stand on them", () => {
    const filter = mount(FilterButton);
    expect(filter.get("button").attributes("aria-label")).toBe("Фильтры");
    expect(filter.get(".filter-btn-label").text()).toBe("Фильтры");

    const sort = mount(SortButton, {
      props: {
        options: [{ value: "created", label: "Дата создания" }],
        sortBy: "created",
        sortOrder: "desc" as const,
      },
    });
    expect(sort.get(".sort-btn").attributes("aria-label")).toBe(
      "Сортировка: Дата создания",
    );
    expect(sort.get(".sort-btn-label").text()).toBe("Дата создания");
  });
});

describe("the bar", () => {
  it("is one flex line, the search first, and keeps no form for a narrow bar or window", () => {
    expect(ruleMap(BAR).get(".probe .filter-bar")).toEqual({
      display: "flex",
      "flex-wrap": "wrap",
      "align-items": "center",
      gap: "8px",
    });
    expect(BAR).not.toMatch(/@container|@media/);
    expect(BAR).not.toMatch(/flex-basis/);
  });

  it("lets the search give way to the buttons beside it, at every width", () => {
    expect(ruleMap(BAR).get(".probe .search-container")?.["min-width"]).toBe(
      "0",
    );
  });
});

describe("the list of a bubble", () => {
  it("shows every name whole: nothing cuts it", () => {
    const css = compileSass(
      '@use "@/assets/styles/Filters" as *\n.probe\n  +filter-bubbles',
    );
    const name = ruleMap(css).get(".probe .owner-name");
    expect(name).toBeDefined();
    expect(name).not.toHaveProperty("text-overflow");
    expect(name).not.toHaveProperty("overflow");
    expect(css).not.toMatch(/text-overflow/);
  });
});

describe("a panel of the bar", () => {
  const ROOM = 375;
  let wrapper: VueWrapper | null = null;

  afterEach(() => {
    wrapper?.unmount();
    wrapper = null;
  });

  /** A panel that the stylesheet has put at [left, left + width). */
  const openAt = async (left: number, width: number) => {
    Object.defineProperty(document.documentElement, "clientWidth", {
      configurable: true,
      get: () => ROOM,
    });
    const panel = ref<HTMLElement | null>(null);
    wrapper = mount(
      defineComponent({
        setup() {
          useKeepInWindow(panel);
          return () => h("div", { ref: panel });
        },
      }),
      { attachTo: document.body },
    );
    await nextTick();
    const el = panel.value!;
    el.getBoundingClientRect = () => {
      // The stylesheet's place, moved by the translate the script sets.
      const shift = Number.parseFloat(el.style.translate || "0") || 0;
      return {
        left: left + shift,
        right: left + width + shift,
        width,
      } as DOMRect;
    };
    window.dispatchEvent(new Event("resize"));
    return el;
  };

  it("is moved back in from the left edge", async () => {
    // The games list on a phone: the panel hung from a button at the left.
    const el = await openAt(-114, 262);
    expect(el.style.translate).toBe(`${WINDOW_EDGE + 114}px 0`);
  });

  it("is moved back in from the right edge", async () => {
    const el = await openAt(275, 262);
    expect(el.style.translate).toBe(`${ROOM - WINDOW_EDGE - 275 - 262}px 0`);
  });

  it("stays where its stylesheet put it when it fits", async () => {
    const el = await openAt(35, 262);
    expect(el.style.translate).toBe("");
  });

  it("stands from the left edge when it is wider than the window", async () => {
    const el = await openAt(-50, 400);
    expect(el.style.translate).toBe(`${WINDOW_EDGE + 50}px 0`);
  });

  it("hangs under its button on every page: no page moves it by a rule of its own", () => {
    // The room hung its sort menu from the left edge of the bar on a phone,
    // where the button stood on a row of its own at the left. The rule took
    // the menu away from its button wherever it stood; what keeps a panel in
    // the window is the script above (useKeepInWindow), the same on every
    // page.
    const src = join(HERE, "../../../..");
    const offenders = readdirSync(src, { recursive: true, encoding: "utf8" })
      .filter((file) => file.endsWith(".vue"))
      .filter((file) =>
        /:deep\(\.sort-(?:dropdown|section)\b/.test(
          readFileSync(join(src, file), "utf8"),
        ),
      )
      .map((file) => file.split("\\").join("/"));
    expect(offenders).toEqual([]);
  });

  it("is never wider than the window less the page's margins", () => {
    const panel = ruleMap(
      compileSass(
        '@use "@/assets/styles/Filters" as *\n.probe\n  +dropdown-panel',
      ),
    ).get(".probe");
    expect(panel?.["max-width"]).toBe("calc(100vw - 32px)");
  });
});
