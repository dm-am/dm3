/**
 * @vitest-environment jsdom
 */

/**
 * The floating column of buttons at the bottom right: "Развернуть все",
 * "Наверх", "Вниз" and the gear of the settings of the site.
 *
 * It stands at every width, as it stood before the wave of 5 October made it
 * fade on a phone. The owner on the bottom of a phone page, 12 October 2026:
 * the buttons are broken and keep disappearing. So nothing takes it away: not
 * the end of a scroll, not the caret in a field, not a side column slid out
 * over it (the scrim of the column lies over it then, and the column holds the
 * caret). The gear is in it at every width, and its bubble holds the settings,
 * which the right column of a phone no longer does.
 *
 * Below the shell breakpoint it stands in the gutter of the page, between the
 * right edge of the text and the scrollbar of the page, so it lies on neither.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useUiStore } from "@/shared/stores/ui";
import { setScrollContainer } from "@/shared/lib/scroll";
import ScrollNav from "./ScrollNav.vue";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

enableAutoUnmount(afterEach);

const HERE = dirname(fileURLToPath(import.meta.url));

const BP_SHELL = Number(
  /\$bp-shell:\s*(\d+)px/.exec(
    readFileSync(
      resolve(HERE, "../../../assets/styles/_Breakpoints.sass"),
      "utf8",
    ),
  )?.[1],
);

/** A window of the given width for the media queries of the shell. */
function windowOf(width: number) {
  const evaluate = (query: string) => {
    const max = /max-width:\s*(\d+)px/.exec(query);
    const min = /min-width:\s*(\d+)px/.exec(query);
    return (
      (!max || width <= Number(max[1])) && (!min || width >= Number(min[1]))
    );
  };
  vi.stubGlobal("matchMedia", (query: string) => ({
    media: query,
    matches: evaluate(query),
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
}

/**
 * The scroll container of the shell, as tall as `content` over `height`, with
 * a scrollbar `scrollbar` wide (none on a phone, where it lies over the page).
 */
function page(content: number, height = 800, scrollbar = 0) {
  const main = document.createElement("div");
  main.className = "main";
  Object.defineProperty(main, "scrollHeight", { value: content });
  Object.defineProperty(main, "clientHeight", { value: height });
  Object.defineProperty(main, "offsetWidth", {
    value: 390,
    configurable: true,
  });
  Object.defineProperty(main, "clientWidth", {
    value: 390 - scrollbar,
    configurable: true,
  });
  document.body.appendChild(main);
  setScrollContainer(main);
  return main;
}

/**
 * The tooltips of the buttons come up on focus and measure their text on a
 * canvas jsdom does not have; what they say is not what this file is about.
 */
const PLAIN_TOOLTIP = { template: "<span><slot /></span>" };

async function mountNav() {
  const wrapper = mount(ScrollNav, {
    attachTo: document.body,
    global: { stubs: { Tooltip: PLAIN_TOOLTIP } },
  });
  await flushPromises();
  return wrapper;
}

type Nav = Awaited<ReturnType<typeof mountNav>>;

const column = (wrapper: Nav) => wrapper.get(".scroll-nav");

/** Up and pressable: drawn, and in no state of the column but its own. */
function expectUp(wrapper: Nav) {
  const element = column(wrapper).element as HTMLElement;
  expect(element.style.display, "the column is taken away").not.toBe("none");
  expect(column(wrapper).classes(), "the column is in a state").toEqual([
    "scroll-nav",
  ]);
  expect(
    wrapper.find('button[aria-label="Прокрутить наверх"]').exists(),
    "no button to the top",
  ).toBe(true);
  expect(
    wrapper.find('button[aria-label="Настройки сайта"]').exists(),
    "no gear",
  ).toBe(true);
}

let main: HTMLElement;

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
});

afterEach(() => {
  main?.remove();
  setScrollContainer(null);
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

for (const width of [1366, 390]) {
  describe(`ScrollNav ${width} wide`, () => {
    beforeEach(() => {
      windowOf(width);
      main = page(3000);
    });

    it("opens the settings of the site from the gear", async () => {
      const wrapper = await mountNav();

      await wrapper
        .get('button[aria-label="Настройки сайта"]')
        .trigger("click");

      const labels = wrapper
        .findAll(".bubble-content .settings-label")
        .map((label) => label.text());
      expect(labels).toEqual(["Верстка", "Тема", "Перенос слов"]);
    });

    it("closes the bubble on Escape and gives the caret back to the gear", async () => {
      const wrapper = await mountNav();
      const gear = wrapper.get('button[aria-label="Настройки сайта"]');
      await gear.trigger("click");

      document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }));
      await flushPromises();

      expect(wrapper.find(".bubble-content").exists()).toBe(false);
      expect(document.activeElement).toBe(gear.element);
    });

    it("closes the bubble on a press outside it", async () => {
      const wrapper = await mountNav();
      await wrapper
        .get('button[aria-label="Настройки сайта"]')
        .trigger("click");

      document.body.click();
      await flushPromises();

      expect(wrapper.find(".bubble-content").exists()).toBe(false);
    });

    it("stands up on a page that has stopped scrolling", async () => {
      vi.useFakeTimers();
      const wrapper = await mountNav();

      main.dispatchEvent(new Event("scroll"));
      vi.advanceTimersByTime(3000);
      await flushPromises();

      expectUp(wrapper);
    });

    it("stands up while a text field holds the caret", async () => {
      const wrapper = await mountNav();
      const field = document.createElement("textarea");
      document.body.appendChild(field);

      field.focus();
      await flushPromises();

      expectUp(wrapper);
      field.remove();
    });

    // The scrim of the column lies over it and the column holds the caret,
    // as before the wave: nothing of its own takes it away.
    it("stands in the page while a side column is out", async () => {
      const wrapper = await mountNav();

      useUiStore().openPanel("left");
      await flushPromises();

      expectUp(wrapper);
    });
  });
}

describe("ScrollNav beside the scrollbar of the page", () => {
  beforeEach(() => windowOf(900));

  // A desktop window narrowed below the shell breakpoint draws a scrollbar of
  // its own on the page; the column steps aside of it instead of lying on it.
  it("knows how wide the scrollbar of the page is", async () => {
    main = page(3000, 800, 15);
    const wrapper = await mountNav();

    expect(
      (column(wrapper).element as HTMLElement).style.getPropertyValue(
        "--page-scrollbar",
      ),
    ).toBe("15px");
  });

  it("measures it again when the window changes", async () => {
    main = page(3000, 800, 15);
    const wrapper = await mountNav();

    Object.defineProperty(main, "clientWidth", { value: 390 });
    window.dispatchEvent(new Event("resize"));
    await flushPromises();

    expect(
      (column(wrapper).element as HTMLElement).style.getPropertyValue(
        "--page-scrollbar",
      ),
    ).toBe("0px");
  });
});

/**
 * Where the column stands and whether it can fade, read off its stylesheet,
 * since jsdom draws nothing.
 */
describe("the stylesheet of the column", () => {
  const css = componentCss(join(HERE, "ScrollNav.vue"));
  const rules = styleRules(css);

  it("has no state in which it is not seen or takes no press", () => {
    const hidden = rules.filter(
      ({ declarations }) =>
        declarations.opacity === "0" ||
        declarations["pointer-events"] === "none" ||
        declarations.visibility === "hidden",
    );
    expect(hidden.map(({ selector }) => selector)).toEqual([]);
  });

  it("stands 16 from the edge of a wide window, as it always stood", () => {
    const own = rules.find(({ selector }) => selector === ".scroll-nav");
    expect(own?.declarations.right).toBe("16px");
  });

  // The gutter of a page below the breakpoint is 32 and the column 24: four
  // from the edge of the page it stands clear of the text by four, and a
  // scrollbar of the page, where there is one, moves it aside by its width.
  it("stands in the gutter of the page below the shell breakpoint", () => {
    expect(BP_SHELL).toBeGreaterThan(0);
    const narrow = new RegExp(
      String.raw`@media \(max-width: ${BP_SHELL}px\)\s*\{\s*\.scroll-nav\s*\{([^}]*)\}`,
    ).exec(css);
    expect(narrow, "no rule for the column below the breakpoint").toBeTruthy();
    const right = /right:\s*([^;]+);/.exec(narrow?.[1] ?? "")?.[1].trim();
    expect(right).toBe("calc(4px + var(--page-scrollbar, 0px))");
  });
});
