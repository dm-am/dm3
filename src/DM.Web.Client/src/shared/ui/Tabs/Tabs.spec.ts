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
import { defineComponent, h, ref, type PropType } from "vue";
import { mount } from "@vue/test-utils";
import Tabs, { type TabItem } from "./Tabs.vue";

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
