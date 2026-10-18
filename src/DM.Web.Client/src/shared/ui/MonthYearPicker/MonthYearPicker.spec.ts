/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach, vi } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { nextTick } from "vue";
import { mount } from "@vue/test-utils";
import { parse as parseSfc } from "vue/compiler-sfc";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import MonthYearPicker from "./MonthYearPicker.vue";

const SFC = join(
  dirname(fileURLToPath(import.meta.url)),
  "MonthYearPicker.vue",
);

describe("MonthYearPicker", () => {
  const mountComponent = (props: Record<string, unknown> = {}) => {
    return mount(MonthYearPicker, {
      attachTo: document.body,
      props: {
        year: 2026,
        month: 7,
        maxYear: 2026,
        maxMonth: 7,
        minYear: 2007,
        ...props,
      },
    });
  };

  const open = async (wrapper: ReturnType<typeof mountComponent>) => {
    await wrapper.find(".myp-trigger").trigger("click");
  };

  // ============================================================================
  // MONTH MODE
  // ============================================================================

  describe("Month mode", () => {
    it("labels the trigger with the capitalized month and year", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".myp-trigger").text()).toBe("Июль 2026");
    });

    it("disables months past maxMonth in the max year", async () => {
      const wrapper = mountComponent();
      await open(wrapper);
      const cells = wrapper.findAll(".myp-grid--months .myp-cell");
      // July (index 6) enabled, August (index 7) disabled in the current year.
      expect(cells[6].attributes("disabled")).toBeUndefined();
      expect(cells[7].attributes("disabled")).toBeDefined();
    });

    it("clamps year navigation at minYear", async () => {
      const wrapper = mountComponent({ minYear: 2025 });
      await open(wrapper);
      const prev = wrapper.find('.myp-nav[aria-label="Предыдущий год"]');
      await prev.trigger("click"); // 2026 -> 2025
      expect(wrapper.find(".myp-year").text()).toBe("2025");
      expect(prev.attributes("disabled")).toBeDefined();
    });

    it("emits year and month when picking in a navigated year", async () => {
      const wrapper = mountComponent();
      await open(wrapper);
      await wrapper
        .find('.myp-nav[aria-label="Предыдущий год"]')
        .trigger("click"); // 2025
      await wrapper.findAll(".myp-grid--months .myp-cell")[2].trigger("click");
      expect(wrapper.emitted("update:year")?.at(-1)).toEqual([2025]);
      expect(wrapper.emitted("update:month")?.at(-1)).toEqual([3]);
    });
  });

  // ============================================================================
  // YEAR MODE (12-year blocks)
  // ============================================================================

  describe("Year mode", () => {
    const yearMode = { mode: "year" as const };

    it("shows the latest 12-year block ascending", async () => {
      const wrapper = mountComponent(yearMode);
      await open(wrapper);
      const years = wrapper
        .findAll(".myp-grid--years .myp-cell")
        .map((c) => Number(c.text()));
      expect(years).toHaveLength(12);
      expect(years[0]).toBe(2015);
      expect(years[11]).toBe(2026);
      expect(wrapper.find(".myp-year").text()).toBe("2015–2026");
    });

    it("pages to a partial oldest block bounded by minYear", async () => {
      const wrapper = mountComponent(yearMode);
      await open(wrapper);
      const prev = wrapper.find('.myp-nav[aria-label="Предыдущие годы"]');
      await prev.trigger("click");
      const years = wrapper
        .findAll(".myp-grid--years .myp-cell")
        .map((c) => Number(c.text()));
      expect(years).toEqual([2007, 2008, 2009, 2010, 2011, 2012, 2013, 2014]);
      expect(prev.attributes("disabled")).toBeDefined();
    });

    it("opens on the block containing the selected year", async () => {
      const wrapper = mountComponent({ ...yearMode, year: 2010 });
      await open(wrapper);
      expect(wrapper.find(".myp-year").text()).toBe("2007–2014");
      expect(wrapper.find(".myp-cell.selected").text()).toBe("2010");
    });

    it("degenerates to a single cell when minYear equals maxYear", async () => {
      const wrapper = mountComponent({
        ...yearMode,
        minYear: 2026,
        maxYear: 2026,
      });
      await open(wrapper);
      const cells = wrapper.findAll(".myp-grid--years .myp-cell");
      expect(cells).toHaveLength(1);
      expect(cells[0].text()).toBe("2026");
      expect(
        wrapper
          .find('.myp-nav[aria-label="Предыдущие годы"]')
          .attributes("disabled"),
      ).toBeDefined();
      expect(
        wrapper
          .find('.myp-nav[aria-label="Следующие годы"]')
          .attributes("disabled"),
      ).toBeDefined();
    });

    it("emits the picked year", async () => {
      const wrapper = mountComponent(yearMode);
      await open(wrapper);
      const cell2020 = wrapper
        .findAll(".myp-grid--years .myp-cell")
        .find((c) => c.text() === "2020")!;
      await cell2020.trigger("click");
      expect(wrapper.emitted("update:year")?.at(-1)).toEqual([2020]);
    });
  });

  // ============================================================================
  // DIALOG BEHAVIOR
  // ============================================================================

  describe("Dialog behavior", () => {
    it("marks the selected cell with aria-current", async () => {
      const wrapper = mountComponent();
      await open(wrapper);
      const selected = wrapper.find(".myp-cell.selected");
      expect(selected.attributes("aria-current")).toBe("date");
    });

    it("closes on pick", async () => {
      const wrapper = mountComponent();
      await open(wrapper);
      await wrapper.findAll(".myp-grid--months .myp-cell")[0].trigger("click");
      expect(wrapper.find(".myp-popover").exists()).toBe(false);
    });
  });

  // ============================================================================
  // POPOVER PLACEMENT
  // ============================================================================

  /**
   * The popover used to be rendered inside the bordered stepper pill, and the
   * pill clips its sections by radius (overflow: hidden). The 220px panel was
   * cut down to the field's own height, and since opening moves focus into a
   * cell, the browser scrolled that clipped box: the field showed a strip of
   * the month grid ("Июл Авг") instead of a popover under it. The border and
   * the clipping live on an inner box now, and the popover is a child of the
   * bare positioning root.
   *
   * The fence reads the component's own stylesheet instead of computed styles
   * because jsdom applies no scoped CSS: getComputedStyle reports "visible"
   * for every element here, so a style-blind test would stay green on exactly
   * the markup that broke.
   */
  describe("Popover placement", () => {
    const SOURCE = readFileSync(
      join(dirname(fileURLToPath(import.meta.url)), "MonthYearPicker.vue"),
      "utf8",
    );

    /**
     * Classes whose rule clips what it contains. Indented Sass: a declaration
     * belongs to the nearest line above it with a smaller indent, so the
     * enclosing selectors form a stack, and the box that clips is the last
     * compound of the chain (in ".a &.b .c" it is ".c", not ".a").
     */
    const clippingClasses = (): Set<string> => {
      const { descriptor } = parseSfc(SOURCE, {
        filename: "MonthYearPicker.vue",
      });
      const clipping = new Set<string>();
      const stack: { indent: number; selector: string }[] = [];
      for (const line of (descriptor.styles[0]?.content ?? "").split("\n")) {
        const text = line.trim();
        if (!text || text.startsWith("//") || text.startsWith("@")) continue;
        const indent = line.length - line.trimStart().length;
        while (stack.length && stack[stack.length - 1].indent >= indent) {
          stack.pop();
        }
        const overflow = /^overflow(?:-[xy])?:\s*(\S+)/.exec(text);
        if (overflow) {
          if (overflow[1] !== "visible") {
            const box =
              stack
                .map((entry) => entry.selector)
                .join(" ")
                .split(/\s+/)
                .pop() ?? "";
            for (const [, name] of box.matchAll(/\.([\w-]+)/g)) {
              clipping.add(name);
            }
          }
          continue;
        }
        // Property lines and mixin calls are leaves; anything else opens a block.
        if (!/^[a-z-]+:/.test(text) && !text.startsWith("+")) {
          stack.push({ indent, selector: text });
        }
      }
      return clipping;
    };

    it("keeps the popover out of every box that clips", async () => {
      const clipping = clippingClasses();
      // A silent empty scan would be worse than a failure: the walk below
      // would then compare against nothing and could never go red again.
      expect(clipping.size).toBeGreaterThan(0);

      const wrapper = mountComponent({ stepper: true });
      await wrapper.find(".myp-trigger-section").trigger("click");
      const popover = wrapper.find(".myp-popover");
      expect(popover.exists()).toBe(true);

      // Up to the component root — boxes above it belong to the page.
      const root = wrapper.element;
      const clipped: string[] = [];
      for (let el = popover.element.parentElement; el; el = el.parentElement) {
        if (Array.from(el.classList).some((name) => clipping.has(name))) {
          clipped.push(el.className);
        }
        if (el === root) break;
      }
      expect(clipped).toEqual([]);
    });

    /**
     * The popover stands in a fixed layer, off the picker's rectangle, as the
     * date field's calendar does. Inside the picker it was cut by any box
     * around it that clips, and on a phone the period of the statistics page
     * is a row that scrolls sideways, which clips what it scrolls.
     *
     * jsdom lays out nothing: the picker's rectangle and the window are given
     * by hand, and the panel, which measures 0 here, is taken at the size the
     * component assumes before it can measure it (220 by 200).
     */
    describe("in a fixed layer", () => {
      const html = document.documentElement;
      let restoreWindow: (() => void) | null = null;

      /** A window of the given size, as the component reads it. */
      const windowOf = (width: number, height: number) => {
        Object.defineProperty(html, "clientWidth", {
          configurable: true,
          get: () => width,
        });
        Object.defineProperty(html, "clientHeight", {
          configurable: true,
          get: () => height,
        });
        restoreWindow = () => {
          delete (html as unknown as Record<string, unknown>).clientWidth;
          delete (html as unknown as Record<string, unknown>).clientHeight;
        };
      };

      afterEach(() => {
        restoreWindow?.();
        restoreWindow = null;
      });

      // The template opens with a comment, so the wrapper's element is that.
      const rootOf = (wrapper: ReturnType<typeof mountComponent>) =>
        wrapper.get(".month-year-picker").element;

      /** The picker at a place on the screen, 38 high and 200 wide. */
      const placed = (left: number, top: number) => {
        const wrapper = mountComponent();
        const box = { left, top };
        Object.defineProperty(rootOf(wrapper), "getBoundingClientRect", {
          configurable: true,
          value: () => ({
            left: box.left,
            top: box.top,
            right: box.left + 200,
            bottom: box.top + 38,
            width: 200,
            height: 38,
          }),
        });
        return { wrapper, box };
      };

      const where = (wrapper: ReturnType<typeof mountComponent>) => {
        const style = (wrapper.find(".myp-popover").element as HTMLElement)
          .style;
        return { top: style.top, left: style.left };
      };

      it("is a fixed layer in the stylesheet, placed by the script alone", () => {
        const rules = ruleMap(componentCss(SFC));
        expect(rules.get(".myp-popover")?.position).toBe("fixed");
        expect(rules.get(".myp-popover")).not.toHaveProperty("top");
        expect(rules.get(".myp-popover")).not.toHaveProperty("left");
      });

      it("stands under the picker with the gap of every dropdown, left edges together", async () => {
        windowOf(1366, 800);
        const { wrapper } = placed(100, 50);
        await open(wrapper);
        // 50 + 38 + the 2 of POPOVER_GAP.
        expect(where(wrapper)).toEqual({ top: "90px", left: "100px" });
      });

      it("stands above the picker where the window has no room below", async () => {
        windowOf(1366, 300);
        const { wrapper } = placed(100, 212);
        await open(wrapper);
        // 212 - 2 - 200.
        expect(where(wrapper).top).toBe("10px");
      });

      it("is pushed back inside the window at its right edge", async () => {
        // A phone: the picker has been scrolled toward the right of its row.
        windowOf(375, 800);
        const { wrapper } = placed(300, 50);
        await open(wrapper);
        // 375 - the 8 of VIEWPORT_EDGE - 220.
        expect(where(wrapper).left).toBe("147px");
      });

      it("is pushed back inside the window at its left edge", async () => {
        windowOf(375, 800);
        const { wrapper } = placed(-40, 50);
        await open(wrapper);
        expect(where(wrapper)).toEqual({ top: "90px", left: "8px" });
      });

      it("stands against the bottom edge where there is room neither below nor above", async () => {
        windowOf(1366, 300);
        const { wrapper } = placed(100, 120);
        await open(wrapper);
        // Below runs past 292, above starts at -82: 300 - 8 - 200.
        expect(where(wrapper).top).toBe("92px");
      });

      it("follows the picker while it is open, and stops listening when it closes", async () => {
        windowOf(1366, 800);
        const { wrapper, box } = placed(100, 50);
        await open(wrapper);
        // The row scrolled sideways under the picker.
        box.left = 40;
        document.dispatchEvent(new Event("scroll"));
        await nextTick();
        expect(where(wrapper).left).toBe("40px");

        await wrapper.find(".myp-trigger").trigger("click");
        expect(wrapper.find(".myp-popover").exists()).toBe(false);
        // Closed, a scroll of the page no longer reads the picker's box.
        const reads = vi.spyOn(rootOf(wrapper), "getBoundingClientRect");
        document.dispatchEvent(new Event("scroll"));
        window.dispatchEvent(new Event("resize"));
        expect(reads).not.toHaveBeenCalled();
      });
    });
  });
});
