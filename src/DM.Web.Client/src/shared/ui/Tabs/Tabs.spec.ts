/**
 * @vitest-environment jsdom
 */

/**
 * Keyboard traversal of the tab strip.
 *
 * The strip renders active-first: the selected tab is always at rendered
 * index 0. Stepping by rendered position is therefore a dead end — arrows
 * bounce between two tabs, Home reselects the current one, and every tab
 * past the second is unreachable from the keyboard. These tests pin the
 * traversal to the declared order, and pin the position the strip reports
 * to assistive tech to that same order.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { defineComponent, h, ref, type PropType } from "vue";
import { mount } from "@vue/test-utils";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import Tabs, { type TabItem } from "./Tabs.vue";

const HERE = dirname(fileURLToPath(import.meta.url));

/**
 * Text as a line box draws it: runs of white space are one space, and none
 * stands at the start or the end of the line (the label of a tab is set on a
 * line of its own in the template).
 */
const drawn = (text: string | null) => (text ?? "").replace(/s+/g, " ").trim();

const ALL: TabItem[] = [
  { value: "about", label: "О себе" },
  { value: "games", label: "Игры" },
  { value: "blogs", label: "Блоги" },
  { value: "topics", label: "Топики" },
  { value: "achievements", label: "Зал славы" },
];

// Mirrors a real caller (pages/profile/ProfilePage.vue): v-model is applied
// inside the handler, so the strip reorders in the same tick as the
// selection — the timing the focus move depends on.
const Host = defineComponent({
  props: {
    tabs: { type: Array as PropType<TabItem[]>, required: true },
    initial: { type: String, required: true },
  },
  setup(props) {
    const active = ref(props.initial);
    return () =>
      h(Tabs, {
        modelValue: active.value,
        tabs: props.tabs,
        idPrefix: "t",
        "onUpdate:modelValue": (value: string) => {
          active.value = value;
        },
      });
  },
});

describe("Tabs keyboard navigation", () => {
  const mountTabs = (initial: string, tabs: TabItem[] = ALL) =>
    mount(Host, { attachTo: document.body, props: { tabs, initial } });

  type Strip = ReturnType<typeof mountTabs>;

  /** Id of the tab the strip currently reports as selected. */
  const selected = (strip: Strip) =>
    strip.find('[role="tab"][aria-selected="true"]').attributes("id");

  /** Presses a key on the only tab in the Tab cycle — the selected one. */
  const press = async (strip: Strip, key: string) => {
    await strip.find('[role="tab"][tabindex="0"]').trigger("keydown", { key });
    return selected(strip);
  };

  const walk = async (strip: Strip, key: string, steps: number) => {
    const seen: (string | undefined)[] = [];
    for (let i = 0; i < steps; i++) seen.push(await press(strip, key));
    return seen;
  };

  it("walks the whole declared order rightwards and wraps", async () => {
    const strip = mountTabs("about");
    expect(await walk(strip, "ArrowRight", 5)).toEqual([
      "t-tab-games",
      "t-tab-blogs",
      "t-tab-topics",
      "t-tab-achievements",
      "t-tab-about",
    ]);
  });

  it("walks the whole declared order leftwards and wraps", async () => {
    const strip = mountTabs("about");
    expect(await walk(strip, "ArrowLeft", 5)).toEqual([
      "t-tab-achievements",
      "t-tab-topics",
      "t-tab-blogs",
      "t-tab-games",
      "t-tab-about",
    ]);
  });

  it("selects the first declared tab on Home", async () => {
    const strip = mountTabs("blogs");
    expect(await press(strip, "Home")).toBe("t-tab-about");
  });

  it("selects the last declared tab on End and stays there", async () => {
    const strip = mountTabs("about");
    expect(await press(strip, "End")).toBe("t-tab-achievements");
    expect(await press(strip, "End")).toBe("t-tab-achievements");
  });

  it("steps over a hidden tab", async () => {
    const tabs = ALL.map((tab) =>
      tab.value === "blogs" ? { ...tab, hidden: true } : tab,
    );
    const strip = mountTabs("games", tabs);
    expect(await press(strip, "ArrowRight")).toBe("t-tab-topics");
  });

  it("moves focus onto the selected tab once the strip has reordered", async () => {
    const strip = mountTabs("about");
    await press(strip, "ArrowRight");
    const focused = document.activeElement;
    expect(focused?.getAttribute("id")).toBe("t-tab-games");
    expect(focused?.getAttribute("aria-selected")).toBe("true");
  });

  it("reports the declared position to assistive tech, not the rendered one", async () => {
    const strip = mountTabs("topics");
    const buttons = strip.findAll('[role="tab"]');
    // Rendered active-first: the selected tab comes first, but announces
    // itself as the fourth of five.
    expect(buttons[0].attributes("id")).toBe("t-tab-topics");
    expect(buttons[0].attributes("aria-posinset")).toBe("4");
    expect(buttons[0].attributes("aria-setsize")).toBe("5");
    expect(buttons[1].attributes("id")).toBe("t-tab-about");
    expect(buttons[1].attributes("aria-posinset")).toBe("1");
  });
});

/**
 * The strip whole at every width, as it stood before 5 October 2026: every tab
 * in it, the open one first, and where the column is narrow it takes the lines
 * it needs. That wave kept it one line by sending the tabs that did not fit
 * into "ЕЩЕ", which hid "Топики" and "Зал славы" on a phone; the owner, 12
 * October 2026, wants nothing shortened and no row scrolled sideways.
 *
 * The line breaks after a bar only, the way the strip of the forum's boards
 * breaks (pages/forum/BoardNavigation.vue): a tab and the bar after it are one
 * item that never breaks, the space after the bar stands between the items, so
 * no line of the strip opens with "|".
 */
describe("Tabs whole at every width", () => {
  const SOURCE = resolve(HERE, "Tabs.vue");

  const mountStrip = (initial: string, tabs: TabItem[] = ALL) =>
    mount(Host, { attachTo: document.body, props: { tabs, initial } });

  it("draws every tab, the open one first, and nothing called Еще", () => {
    const strip = mountStrip("topics");

    expect(strip.findAll('[role="tab"]').map((tab) => tab.text())).toEqual([
      "Топики",
      "О себе",
      "Игры",
      "Блоги",
      "Зал славы",
    ]);
    expect(strip.find("[hidden]").exists()).toBe(false);
    expect(strip.find(".more-menu").exists()).toBe(false);
    expect(strip.text()).not.toMatch(/Еще|ЕЩЕ|…/);
    strip.unmount();
  });

  it("breaks a line after a bar only, never before one", () => {
    const strip = mountStrip("about");
    const list = strip.get('[role="tablist"]').element;

    // A tab and the bar after it are one item; the space after the bar is
    // outside it, the one place the line can break.
    expect(
      strip
        .findAll(".tabs__item")
        .map((item) => drawn(item.element.textContent)),
    ).toEqual(["О себе |", "Игры |", "Блоги |", "Топики |", "Зал славы"]);
    const between = [...list.children]
      .filter((el) => !el.classList.contains("tabs__item"))
      .map((el) => el.textContent);
    expect(between).toEqual([" ", " ", " ", " "]);
    for (const one of strip.findAll(".separator"))
      expect(one.attributes("aria-hidden")).toBe("true");
    expect(list.textContent).not.toContain(" ");

    const rules = ruleMap(componentCss(SOURCE));
    expect(rules.get(".tabs__item")?.["white-space"]).toBe("nowrap");
    strip.unmount();
  });

  it("copies as one line of words with a bar between each two", () => {
    const strip = mountStrip("games");

    expect(drawn(strip.get('[role="tablist"]').element.textContent)).toBe(
      "Игры | О себе | Блоги | Топики | Зал славы",
    );
    strip.unmount();
  });

  it("holds tabs in its tablist and nothing else a reader is offered", () => {
    const strip = mountStrip("about");
    const list = strip.get('[role="tablist"]');

    expect(list.classes()).toContain("tabs");
    expect(
      list.findAll("button").every((b) => b.attributes("role") === "tab"),
    ).toBe(true);
    expect(list.findAll("button")).toHaveLength(ALL.length);
    strip.unmount();
  });

  it("wraps like text at every width and never scrolls sideways", () => {
    const css = componentCss(SOURCE);
    const strip = ruleMap(css).get(".tabs") ?? {};

    expect(strip.display).toBe("block");
    for (const property of ["overflow", "overflow-x", "white-space"])
      expect(strip[property], property).toBeUndefined();
    expect(ruleMap(css).get(".tab")?.["white-space"]).toBe("nowrap");
    expect(ruleMap(css).get(".tab")?.display).toBe("inline-block");
    // No @media takes a phone's strip anywhere else, and nothing measures it
    // to send a tab away.
    expect(css).not.toMatch(/@media|@container/);
    expect(readFileSync(SOURCE, "utf8")).not.toMatch(
      /FitRow|MoreMenu|data-fit-/,
    );
  });
});
