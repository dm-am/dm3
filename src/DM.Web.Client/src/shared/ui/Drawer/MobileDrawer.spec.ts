/**
 * @vitest-environment jsdom
 */

/**
 * The sliding column of a narrow screen, slid in over the page from the left
 * edge. It holds the shell's columns themselves and not copies, so it is on
 * the page the whole time: what changes is whether it is out, and out it is a
 * modal with everything a modal owes.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, enableAutoUnmount } from "@vue/test-utils";
import { nextTick } from "vue";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import MobileDrawer from "./MobileDrawer.vue";
import { componentCss, ruleMap, styleRules } from "@/shared/lib/testing/styles";
import { setScrollContainer } from "@/shared/lib/scroll";

const HERE = dirname(fileURLToPath(import.meta.url));

enableAutoUnmount(afterEach);

/**
 * The component's own stylesheet, as the browser gets it.
 *
 * jsdom applies no scoped styles and vitest never compiles a <style> block, so
 * a layout rule is invisible to an ordinary mount: the close button sat inside
 * the scroller for as long as it did because every assertion about it was
 * about markup, and markup was not where it went wrong. Compiling the block
 * turns the rule into something a test can read.
 */
const panelStyles = (): string =>
  componentCss(resolve(HERE, "MobileDrawer.vue"));

/** Selectors whose rule makes the element a scroll container. */
const scrollingSelectors = (css: string): string[] =>
  styleRules(css)
    .filter(({ declarations }) =>
      [declarations.overflow, declarations["overflow-y"]].some((value) =>
        /^(auto|scroll)\b/.test(value ?? ""),
      ),
    )
    .map(({ selector }) => selector);

/** Mounts a column; `attach` puts it in the document, which focus needs. */
function column(
  props: { open: boolean },
  content = "<a href='#first'>Первый раздел</a>",
  attach = false,
) {
  return mount(MobileDrawer, {
    props: { label: "Меню", ...props },
    slots: { default: content },
    attachTo: attach ? document.body : undefined,
  });
}

const closedWith = (wrapper: ReturnType<typeof column>) =>
  wrapper.emitted("update:open");

/**
 * Whether the column is inert. A browser takes `inert` as a property of the
 * element; jsdom has no such property, so Vue writes the bound value out as
 * the attribute, "false" included.
 */
const isInert = (wrapper: ReturnType<typeof column>) => {
  const value = wrapper.find("aside").attributes("inert");
  return value !== undefined && value !== "false";
};

describe("MobileDrawer", () => {
  // The column holds the page still on the app's real scroll container
  // (App.vue's `.main`, published through the scroll registry).
  let mainEl: HTMLDivElement;

  beforeEach(() => {
    mainEl = document.createElement("div");
    mainEl.className = "main";
    mainEl.style.overflow = "scroll";
    document.body.appendChild(mainEl);
    setScrollContainer(mainEl);
  });

  afterEach(() => {
    setScrollContainer(null);
    document.body.removeChild(mainEl);
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  describe("one column, in or out", () => {
    it("keeps what it holds on the page while it is in", () => {
      const wrapper = column(
        { open: false },
        "<div class='held'>Мои игры</div>",
      );

      expect(wrapper.find(".held").exists()).toBe(true);
      expect(wrapper.find(".drawer-scrim").exists()).toBe(false);
    });

    it("takes no focus and no press while it is in", () => {
      const wrapper = column({ open: false });

      expect(isInert(wrapper)).toBe(true);
    });

    it("dims the page under it while it is out", () => {
      const wrapper = column({ open: true });

      expect(wrapper.find(".drawer-scrim").exists()).toBe(true);
      expect(isInert(wrapper)).toBe(false);
    });

    it("stands at the left edge, most of the screen wide", () => {
      const panel = ruleMap(panelStyles()).get(".drawer-panel");

      expect(panel?.left).toBe("0");
      expect(panel?.right).toBeUndefined();
      // 85% of the screen, and of the 706 a phone lays the page out at
      // (index.html) on a tablet: no wider than 600.
      expect(panel?.width).toBe("min(85vw, 600px)");
      expect(column({ open: true }).find("aside").classes()).not.toContain(
        "drawer-panel--right",
      );
    });

    it("hands the shell's class to the column, not to the dimming", () => {
      const wrapper = mount(MobileDrawer, {
        props: { open: true, label: "Меню" },
        attrs: { class: "sidebar-left" },
      });

      expect(wrapper.find("aside").classes()).toContain("sidebar-left");
      expect(wrapper.find(".drawer-scrim").classes()).not.toContain(
        "sidebar-left",
      );
    });
  });

  describe("how it is drawn", () => {
    const rules = () => ruleMap(panelStyles());

    // In, it is off the screen by its own width and hidden, so neither its
    // shadow nor its content shows at the edge.
    it("slides out past its own edge while it is in", () => {
      expect(rules().get(".drawer-panel")?.visibility).toBe("hidden");
      expect(rules().get(".drawer-panel")?.transform).toBe("translateX(-100%)");
    });

    it("comes in whole while it is out", () => {
      const open = rules().get(".drawer-panel--open");

      expect(open?.visibility).toBe("visible");
      expect(open?.transform).toBe("none");
    });

    it("keeps the scroll of the column off the page under it", () => {
      expect(rules().get(".drawer-content")?.["overscroll-behavior"]).toBe(
        "contain",
      );
    });

    it("keeps the close button out of every scrolling region", () => {
      const wrapper = column(
        { open: true },
        "<div style='height:3000px'>long menu</div>",
      );
      const closeEl = wrapper.find(".drawer-close").element;

      const selectors = scrollingSelectors(panelStyles());
      // A column with nothing scrollable would pass the loop below by doing
      // nothing at all.
      expect(selectors.length).toBeGreaterThan(0);

      for (const selector of selectors) {
        const scroller = wrapper.find("aside").element.querySelector(selector);
        expect(scroller, `${selector} is not in the column`).not.toBeNull();
        // Pinned inside the scroller the button travelled away with the
        // content, and a menu longer than the screen could only be closed by
        // the dimming or Escape.
        expect(
          scroller!.contains(closeEl),
          `${selector} scrolls and carries the close button`,
        ).toBe(false);
      }
    });

    it("puts the close button ahead of the content, so it is the first tab stop", () => {
      const wrapper = column({ open: true });

      const focusable = wrapper
        .find("aside")
        .element.querySelectorAll("button, [href]");
      expect(focusable[0]).toBe(wrapper.find(".drawer-close").element);
    });

    // The owner could not tell what the column was or where it ended: it
    // opened on a strip with nothing but a typed "×" in it (5 October, lines
    // 2-3). The strip carries the column's name now, and it stays where the
    // close button stays, out of the scroller.
    it("names the column in its bar when it is given nothing else there", () => {
      const wrapper = column({ open: true }, "<div>long menu</div>");
      const title = wrapper.find(".drawer-header h2");

      expect(title.exists()).toBe(true);
      expect(title.text()).toBe("Меню");
      const selectors = scrollingSelectors(panelStyles());
      expect(selectors.length).toBeGreaterThan(0);
      for (const selector of selectors) {
        const scroller = wrapper.find("aside").element.querySelector(selector);
        expect(
          scroller!.contains(title.element),
          `${selector} scrolls and carries the name`,
        ).toBe(false);
      }
    });

    // The shell puts the tabs of its two views there.
    it("holds what it is given in its bar, outside every scrolling region", () => {
      const wrapper = mount(MobileDrawer, {
        props: { open: true, label: "Меню" },
        slots: {
          bar: "<div class='views' role='tablist'>Меню | О сайте</div>",
          default: "<div>long menu</div>",
        },
      });
      const views = wrapper.find(".drawer-header .views");

      expect(views.exists()).toBe(true);
      expect(wrapper.find(".drawer-header h2").exists()).toBe(false);
      for (const selector of scrollingSelectors(panelStyles())) {
        const scroller = wrapper.find("aside").element.querySelector(selector);
        expect(
          scroller!.contains(views.element),
          `${selector} scrolls and carries the bar`,
        ).toBe(false);
      }
      // The cross stays in its corner beside them.
      expect(wrapper.find(".drawer-header .drawer-close").exists()).toBe(true);
    });

    // The cross of a window (_DialogSurface.sass, =dialog-close): two bars the
    // stylesheet draws. A glyph inside would stand behind them, and a typed
    // cross is thinner than the marks around it.
    it("draws the cross of a window, not a typed character", () => {
      const wrapper = column({ open: true });
      const close = rules().get(".drawer-close");

      expect(wrapper.find(".drawer-close").text()).toBe("");
      expect(close?.position).toBe("absolute");
      expect(close?.width).toBe("32px");
      expect(close?.height).toBe("32px");
    });

    it("parts its bar from the content with a line of the theme", () => {
      expect(rules().get(".drawer-header")?.["border-bottom"]).toBe(
        "1px solid var(--border)",
      );
    });

    // The windows of the site dim the page with no blur, and so does the
    // column: what lies under it is the page the reader just looked at.
    it("dims the page under it without blurring it", () => {
      expect(rules().get(".drawer-scrim")?.["backdrop-filter"]).toBeUndefined();
    });
  });

  describe("as a modal", () => {
    // The name of the button that opens it, whatever its bar holds: the tabs
    // of its views name the views, not the column.
    it("is named after the button that opens it", () => {
      const panel = column({ open: true }).find("aside");

      expect(panel.attributes("role")).toBe("dialog");
      expect(panel.attributes("aria-modal")).toBe("true");
      expect(panel.attributes("aria-label")).toBe("Меню");
    });

    it("names its close button", () => {
      expect(
        column({ open: true }).find(".drawer-close").attributes("aria-label"),
      ).toBe("Закрыть");
    });

    it("moves focus to the close button when it comes out, and back when it goes", async () => {
      const opener = document.createElement("button");
      document.body.appendChild(opener);
      opener.focus();
      const wrapper = column({ open: false }, undefined, true);

      await wrapper.setProps({ open: true });
      await nextTick();
      await nextTick();
      expect(document.activeElement).toBe(
        wrapper.find(".drawer-close").element,
      );

      await wrapper.setProps({ open: false });
      await nextTick();
      expect(document.activeElement).toBe(opener);
      opener.remove();
    });

    it("wraps Shift+Tab from the close button round to the last focusable", async () => {
      const wrapper = column(
        { open: false },
        "<a href='#last'>Последний раздел</a>",
        true,
      );

      await wrapper.setProps({ open: true });
      await nextTick();
      await nextTick();

      await wrapper.find("aside").trigger("keydown", {
        key: "Tab",
        shiftKey: true,
      });

      expect(document.activeElement).toBe(
        wrapper.find("a[href='#last']").element,
      );
    });
  });

  describe("ways back in", () => {
    it("goes back on the close button", async () => {
      const wrapper = column({ open: true });

      await wrapper.find(".drawer-close").trigger("click");

      expect(closedWith(wrapper)).toEqual([[false]]);
    });

    it("goes back on a press on the dimmed page", async () => {
      const wrapper = column({ open: true });

      await wrapper.find(".drawer-scrim").trigger("click");

      expect(closedWith(wrapper)).toEqual([[false]]);
    });

    it("goes back on Escape", async () => {
      const wrapper = column({ open: true });

      await wrapper.find("aside").trigger("keydown", { key: "Escape" });

      expect(closedWith(wrapper)).toEqual([[false]]);
    });

    // A link to the page already open leaves the address as it was, and a
    // shell that closed only on a new address left the column standing.
    it("goes back on a press on any link inside", async () => {
      const wrapper = column(
        { open: true },
        "<a href='#room'><span class='inner'>Комната</span></a>",
      );

      await wrapper.find(".inner").trigger("click");

      expect(closedWith(wrapper)).toEqual([[false]]);
    });

    it("stays out on a press that is not a link", async () => {
      const wrapper = column(
        { open: true },
        "<button type='button' class='vote'>Проголосовать</button><p class='text'>Опрос</p>",
      );

      await wrapper.find(".vote").trigger("click");
      await wrapper.find(".text").trigger("click");

      expect(closedWith(wrapper)).toBeUndefined();
    });
  });

  describe("the back gesture", () => {
    /** A CloseWatcher that records itself, as Android's browsers have one. */
    function stubCloseWatcher() {
      const made: { onclose: (() => void) | null; destroy: () => void }[] = [];
      class Watcher {
        onclose: (() => void) | null = null;
        destroy = vi.fn();
        constructor() {
          made.push(this);
        }
      }
      vi.stubGlobal("CloseWatcher", Watcher);
      return made;
    }

    it("closes the column instead of leaving the page", async () => {
      const made = stubCloseWatcher();
      const wrapper = column({ open: false });

      await wrapper.setProps({ open: true });
      expect(made).toHaveLength(1);

      made[0].onclose?.();
      expect(closedWith(wrapper)).toEqual([[false]]);
    });

    it("lets the gesture go once the column is in", async () => {
      const made = stubCloseWatcher();
      const wrapper = column({ open: true });

      await wrapper.setProps({ open: false });

      expect(made[0].destroy).toHaveBeenCalledTimes(1);
    });

    it("does without where the browser has no CloseWatcher", async () => {
      vi.stubGlobal("CloseWatcher", undefined);
      const wrapper = column({ open: false });

      await wrapper.setProps({ open: true });
      await wrapper.find("aside").trigger("keydown", { key: "Escape" });

      expect(closedWith(wrapper)).toEqual([[false]]);
    });
  });

  describe("the page under it", () => {
    it("is held still while the column is out", async () => {
      column({ open: true });
      await nextTick();

      expect(mainEl.style.overflow).toBe("hidden");
    });

    it("scrolls again once the column is in", async () => {
      const wrapper = column({ open: true });
      await nextTick();

      await wrapper.setProps({ open: false });
      await nextTick();

      expect(mainEl.style.overflow).toBe("scroll");
    });

    it("is not held while the column has stayed in", () => {
      column({ open: false });

      expect(mainEl.style.overflow).toBe("scroll");
    });
  });
});
