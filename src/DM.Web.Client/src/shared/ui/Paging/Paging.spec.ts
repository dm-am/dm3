/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { compileSass, styleRules } from "@/shared/lib/testing/styles";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import Paging from "./Paging.vue";
import {
  PAGES_FEWEST,
  PAGES_WIDE,
  pagesThatFit,
  pageWindow,
} from "./pageWindow";
import { scrollBlockIntoView, scrollContentToTop } from "@/shared/lib/scroll";

// Mock vue-router
vi.mock("vue-router", () => ({
  useRoute: () => ({
    query: {},
  }),
}));

// Mock the scroll helpers so click behavior can be asserted
vi.mock("@/shared/lib/scroll", () => ({
  scrollBlockIntoView: vi.fn(),
  scrollContentToTop: vi.fn(),
}));

// Mock Tooltip component
const TooltipStub = {
  template: "<span><slot /></span>",
  props: ["text"],
};

describe("Paging", () => {
  const defaultPaging = {
    pages: 10,
    current: 5,
    skip: 80,
    take: 20,
    total: 200,
  };

  const defaultTo = {
    name: "test-route",
    params: { id: "123" },
  };

  const mountPaging = (props = {}) => {
    return mount(Paging, {
      props: {
        paging: defaultPaging,
        to: defaultTo,
        useQuery: true,
        ...props,
      },
      global: {
        stubs: {
          RouterLink: RouterLinkStub,
          Tooltip: TooltipStub,
        },
      },
    });
  };

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders when pages > 1", () => {
      const wrapper = mountPaging();
      expect(wrapper.find(".paging").exists()).toBe(true);
    });

    it("does not render when pages <= 1", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 1 },
      });
      expect(wrapper.find(".paging").exists()).toBe(false);
    });

    it("renders page numbers", () => {
      const wrapper = mountPaging();
      const pageLinks = wrapper.findAll(".page-number");
      expect(pageLinks.length).toBeGreaterThan(0);
    });

    it("highlights current page as active", () => {
      const wrapper = mountPaging();
      const activeLink = wrapper.find(".page-number.active");
      expect(activeLink.exists()).toBe(true);
      expect(activeLink.text()).toBe("5");
    });
  });

  // ============================================================================
  // NAVIGATION BUTTONS
  // ============================================================================

  describe("Navigation Buttons", () => {
    it("shows first page button (<<) when not near start", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 20, current: 15 },
      });
      const navButtons = wrapper.findAll(".nav-button");
      const firstButton = navButtons.find((btn) => btn.text() === "<<");
      expect(firstButton).toBeDefined();
    });

    it("shows last page button (>>) when not near end", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 20, current: 5 },
      });
      const navButtons = wrapper.findAll(".nav-button");
      const lastButton = navButtons.find((btn) => btn.text() === ">>");
      expect(lastButton).toBeDefined();
    });

    it("shows ellipsis (...) for navigation jumps", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 20, current: 10 },
      });
      const navButtons = wrapper.findAll(".nav-button");
      const ellipsisButtons = navButtons.filter((btn) => btn.text() === "...");
      expect(ellipsisButtons.length).toBeGreaterThan(0);
    });

    it("hides << and >> when total pages <= 10", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 8, current: 4 },
      });
      const navButtons = wrapper.findAll(".nav-button");
      expect(navButtons.length).toBe(0);
    });
  });

  // ============================================================================
  // ACCESSIBLE NAMES
  // ============================================================================

  /**
   * The strip is punctuation and bare numbers: "<<", "...", ">>", "17". A
   * reader announces those as "ссылка меньше меньше" and "ссылка семнадцать",
   * so every link carries its own aria-label, and the tooltip beside it only
   * describes. accessibleNames.spec.ts catches the punctuation links over the
   * whole tree; a page number is an expression and is decidable only once
   * rendered, which is what these tests are for.
   */
  describe("Accessible Names", () => {
    /** Window 16-25 of 40, so both jump pairs are on screen. */
    const wide = () =>
      mountPaging({ paging: { ...defaultPaging, pages: 40, current: 20 } });

    /** What a reader announces: the label wins over the content. */
    const announced = (selector: string): string[] =>
      wide()
        .findAll(selector)
        .map((link) => link.attributes("aria-label") ?? link.text());

    it("names every link in the strip", () => {
      const names = announced("a");
      expect(names.length).toBe(14);
      for (const name of names) expect(name).toMatch(/[\p{L}\p{N}]/u);
    });

    it("names a page link by its page", () => {
      expect(announced(".page-number")).toEqual(
        Array.from({ length: 10 }, (_, i) => `Страница ${16 + i}`),
      );
    });

    it("names a jump link by where it goes", () => {
      expect(announced(".nav-button")).toEqual([
        "Первая страница",
        "Назад",
        "Вперед",
        "Последняя страница",
      ]);
    });
  });

  // ============================================================================
  // PAGE WINDOW CALCULATION
  // ============================================================================

  describe("Page Window", () => {
    it("shows all pages when total <= 10", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 8, current: 4 },
      });
      const pageLinks = wrapper.findAll(".page-number");
      expect(pageLinks.length).toBe(8);
    });

    it("shows pages 1-10 when near start", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 20, current: 3 },
      });
      const pageLinks = wrapper.findAll(".page-number");
      expect(pageLinks[0].text()).toBe("1");
      expect(pageLinks[pageLinks.length - 1].text()).toBe("10");
    });

    it("shows last 10 pages when near end", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 20, current: 18 },
      });
      const pageLinks = wrapper.findAll(".page-number");
      expect(pageLinks[0].text()).toBe("11");
      expect(pageLinks[pageLinks.length - 1].text()).toBe("20");
    });

    it("centers window around current page in middle", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 20, current: 10 },
      });
      const pageLinks = wrapper.findAll(".page-number");
      const pageNumbers = pageLinks.map((link) => parseInt(link.text()));
      expect(pageNumbers).toContain(10);
      expect(pageNumbers.length).toBe(10);
    });
  });

  // ============================================================================
  // QUERY-BASED PAGINATION
  // ============================================================================

  describe("Query-based Pagination", () => {
    it("uses queryKey prop for link generation", () => {
      const wrapper = mountPaging({
        useQuery: true,
        queryKey: "page",
      });
      // Component should use the queryKey for generating links
      expect(wrapper.props("queryKey")).toBe("page");
    });

    it("defaults queryKey to 'number'", () => {
      const wrapper = mountPaging({ useQuery: true });
      expect(wrapper.props("queryKey")).toBe("number");
    });
  });

  // ============================================================================
  // OPTIMISTIC UPDATE
  // ============================================================================

  describe("Optimistic Update", () => {
    beforeEach(() => {
      vi.mocked(scrollBlockIntoView).mockClear();
      vi.mocked(scrollContentToTop).mockClear();
    });

    it("updates current page immediately on click", async () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, current: 1 },
      });

      const pageLink = wrapper.find(".page-number:not(.active)");
      await pageLink.trigger("click");

      // Should update internal state optimistically
      const activeLink = wrapper.find(".page-number.active");
      expect(activeLink.exists()).toBe(true);
    });

    it("scrolls the content to top when no scroll anchor is set", async () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, current: 1 },
      });

      await wrapper.find(".page-number:not(.active)").trigger("click");

      expect(scrollContentToTop).toHaveBeenCalled();
      expect(scrollBlockIntoView).not.toHaveBeenCalled();
    });

    it("scrolls the anchored block into view when scrollAnchor is set", async () => {
      const anchorEl = document.createElement("div");
      const wrapper = mountPaging({
        paging: { ...defaultPaging, current: 1 },
        scrollAnchor: () => anchorEl,
      });

      await wrapper.find(".page-number:not(.active)").trigger("click");

      expect(scrollBlockIntoView).toHaveBeenCalledWith(anchorEl);
      expect(scrollContentToTop).not.toHaveBeenCalled();
    });

    it("falls back to content top when the anchor getter returns null", async () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, current: 1 },
        scrollAnchor: () => null,
      });

      await wrapper.find(".page-number:not(.active)").trigger("click");

      expect(scrollContentToTop).toHaveBeenCalled();
      expect(scrollBlockIntoView).not.toHaveBeenCalled();
    });
  });

  // ============================================================================
  // EDGE CASES
  // ============================================================================

  describe("Edge Cases", () => {
    it("handles single page gracefully", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 1, current: 1 },
      });
      expect(wrapper.find(".paging").exists()).toBe(false);
    });

    it("handles zero pages", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 0, current: 0 },
      });
      expect(wrapper.find(".paging").exists()).toBe(false);
    });

    it("handles large page numbers", () => {
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 1000, current: 500 },
      });
      expect(wrapper.find(".paging").exists()).toBe(true);
      const pageLinks = wrapper.findAll(".page-number");
      expect(pageLinks.length).toBe(10);
    });
  });
  // ============================================================================
  // FITTING THE LINE
  // ============================================================================

  /**
   * One line at any width, with the dots (owner, 5 October 2026: "if it does
   * not fit, show fewer numbers", after a phone strip that had dropped the
   * dots instead). jsdom lays nothing out, so the line and the face are given:
   * a strip `room` wide whose every character is 8 wide, the sides of a link
   * being 0 here (scoped styles are not applied).
   */
  describe("Fitting the line", () => {
    let room = 0;
    // The callback of the observer, kept per element it was told to watch.
    let watched = new Map<Element, () => void>();

    beforeEach(() => {
      room = 0;
      watched = new Map();
      vi.spyOn(
        HTMLElement.prototype,
        "getBoundingClientRect",
      ).mockImplementation(function (this: HTMLElement) {
        const width = this.classList.contains("paging") ? room : 0;
        return {
          width,
          height: 0,
          top: 0,
          left: 0,
          right: width,
          bottom: 0,
          x: 0,
          y: 0,
          toJSON: () => ({}),
        };
      });
      const pen = {
        font: "",
        measureText: (text: string) => ({ width: text.length * 8 }),
      };
      vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(
        pen as unknown as CanvasRenderingContext2D,
      );
      vi.stubGlobal(
        "ResizeObserver",
        vi.fn(function (callback: () => void) {
          return {
            observe: (element: Element) => watched.set(element, callback),
            unobserve: (element: Element) => watched.delete(element),
            disconnect: vi.fn(),
          };
        }),
      );
    });

    afterEach(() => {
      vi.restoreAllMocks();
      vi.unstubAllGlobals();
    });

    const numbers = (wrapper: ReturnType<typeof mountPaging>) =>
      wrapper.findAll(".page-number").map((link) => Number(link.text()));

    const strip = (wrapper: ReturnType<typeof mountPaging>) =>
      wrapper.text().replace(/\s+/g, " ").trim();

    it("gives up numbers and keeps the dots and the jumps on a short line", async () => {
      // A label of 150 is 24, the four jumps 80: 210 holds five numbers.
      room = 210;
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 150, current: 120 },
      });
      await flushPromises();
      expect(numbers(wrapper)).toEqual([118, 119, 120, 121, 122]);
      expect(strip(wrapper)).toBe("<< ... 118 119 120 121 122 ... >>");
    });

    it("shows ten where the line holds ten", async () => {
      room = 400;
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 150, current: 120 },
      });
      await flushPromises();
      expect(numbers(wrapper)).toEqual([
        116, 117, 118, 119, 120, 121, 122, 123, 124, 125,
      ]);
    });

    it("keeps the current page and one to each side however short the line", async () => {
      room = 100;
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 150, current: 120 },
      });
      await flushPromises();
      expect(strip(wrapper)).toBe("<< ... 119 120 121 ... >>");
    });

    it("measures again when the line changes width", async () => {
      room = 400;
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 150, current: 120 },
      });
      await flushPromises();
      expect(numbers(wrapper)).toHaveLength(10);
      room = 210;
      watched.get(wrapper.get(".paging").element)?.();
      await flushPromises();
      expect(numbers(wrapper)).toHaveLength(5);
    });

    it("shows a short list whole and without jumps when it fits", async () => {
      // Eight one-digit pages take 64 of the 100: no window is needed.
      room = 100;
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 8, current: 1 },
      });
      await flushPromises();
      expect(strip(wrapper)).toBe("1 2 3 4 5 6 7 8");
    });

    it("gives a short list that does not fit whole a window and the jumps", async () => {
      // Eight pages at 8 would take 64 of the 60: the window of three comes in.
      room = 60;
      const wrapper = mountPaging({
        paging: { ...defaultPaging, pages: 8, current: 1 },
      });
      await flushPromises();
      expect(strip(wrapper)).toBe("1 2 3 ... >>");
    });

    it("gives a link the side of the desktop at every width, a phone's too", () => {
      const css = compileSass(
        '@use "@/assets/styles/Paging" as *\n.probe\n  +paging',
      ).replace(/\s+/g, " ");
      // A phone shows the strip of the full version: the stylesheet offers no
      // narrower side, so the fit never marks a strip tight.
      expect(css).not.toMatch(/@media/);
      expect(css).not.toContain("--paging-side-tight");
      const rules = styleRules(css);
      const rule = (selector: string) =>
        rules.find((found) => found.selector === selector)?.declarations;
      expect(rule(".probe .paging")?.["--paging-side"]).toBe("8px");
      expect(rule(".probe .paging a")?.padding).toBe("2px var(--paging-side)");
    });

    /**
     * The side a link takes: the 8 of the desktop at every width, a phone's
     * too, which shows the strip of the full version. The strip holds as many
     * numbers as fit at that side. Given here as the stylesheet gives it: a
     * side of 8 and margins of 4 round each link; every character 8 wide.
     */
    describe("the side of a link", () => {
      beforeEach(() => {
        const real = window.getComputedStyle.bind(window);
        vi.spyOn(window, "getComputedStyle").mockImplementation(
          (element: Element, pseudo?: string | null) => {
            const strip = element.classList.contains("paging");
            if (!strip && !element.closest(".paging")) {
              return real(element, pseudo);
            }
            const values: Record<string, string> = strip
              ? { "--paging-side": "8px" }
              : { "margin-left": "4px", "margin-right": "4px" };
            return {
              getPropertyValue: (name: string) => values[name] ?? "",
              fontSize: "14px",
              fontFamily: "sans-serif",
              fontWeight: "400",
            } as unknown as CSSStyleDeclaration;
          },
        );
      });

      it("shows three pages whole where they fit", async () => {
        room = 326;
        const wrapper = mountPaging({
          paging: { ...defaultPaging, pages: 3, current: 1 },
        });
        await flushPromises();
        expect(strip(wrapper)).toBe("1 2 3");
        expect(wrapper.get(".paging").classes()).toEqual(["paging"]);
      });

      it("holds as many numbers as fit at the side of 8", async () => {
        // A number of 150 is 24 and 24 round it; the jumps 176. 340 holds
        // three, and a fourth would take 368.
        room = 340;
        const wrapper = mountPaging({
          paging: { ...defaultPaging, pages: 150, current: 120 },
        });
        await flushPromises();
        expect(numbers(wrapper)).toEqual([119, 120, 121]);
        room = 380;
        const wider = mountPaging({
          paging: { ...defaultPaging, pages: 150, current: 120 },
        });
        await flushPromises();
        expect(numbers(wider)).toEqual([119, 120, 121, 122]);
      });

      it("fits again when the count of pages changes", async () => {
        // Three pages show whole, and nine of the same one digit show whole
        // too once the count changes: the strip is fitted again for them and
        // does not stay "1 2 3 ... >>".
        room = 330;
        const wrapper = mountPaging({
          paging: { ...defaultPaging, pages: 3, current: 1 },
        });
        await flushPromises();
        expect(strip(wrapper)).toBe("1 2 3");
        await wrapper.setProps({
          paging: { ...defaultPaging, pages: 9, current: 1 },
        });
        await flushPromises();
        expect(strip(wrapper)).toBe("1 2 3 4 5 6 7 8 9");
      });
    });
  });

  describe("pagesThatFit", () => {
    // Measured on the stand, 5 October 2026: a link of a three-digit page is
    // 42.89 wide on a phone (sides of 6) and 46.89 elsewhere, the bold of the
    // current page adds 0.94, and "<<", "...", "...", ">>" take 129.9 and 145.9.
    const phone = { number: 42.89, current: 0.94, jumps: 129.9 };
    const desk = { number: 46.89, current: 0.94, jumps: 145.9 };

    it("holds three numbers of three digits at 360 and at 375, in one line", () => {
      // The line of a list at 360 is 296, under a table 270; at 375 under a
      // table 285.
      for (const room of [270, 285, 296])
        expect(pagesThatFit(150, { ...phone, room }), `${room}`).toBe(3);
    });

    it("holds ten of three digits in the column of 1366", () => {
      // 615.75 of the 616 the strip has under a table there.
      expect(pagesThatFit(150, { ...desk, room: 616 - 0.25 })).toBe(10);
      expect(pagesThatFit(150, { ...desk, room: 615 })).toBe(9);
    });

    it("never goes above ten nor below three", () => {
      expect(pagesThatFit(150, { ...desk, room: 5000 })).toBe(PAGES_WIDE);
      expect(pagesThatFit(150, { ...desk, room: 10 })).toBe(PAGES_FEWEST);
    });

    it("gives a list of ten pages or fewer all of them when they fit", () => {
      expect(pagesThatFit(7, { ...phone, room: 7 * 42.89 + 0.94 })).toBe(7);
      expect(pagesThatFit(7, { ...phone, room: 7 * 42.89 })).toBe(3);
    });
  });

  describe("pageWindow", () => {
    it("is the window of ten the strip always had", () => {
      expect(pageWindow(3, 20, PAGES_WIDE)).toEqual({
        lowerBound: 1,
        upperBound: 10,
      });
      expect(pageWindow(5, 20, PAGES_WIDE)).toEqual({
        lowerBound: 1,
        upperBound: 10,
      });
      expect(pageWindow(6, 20, PAGES_WIDE)).toEqual({
        lowerBound: 2,
        upperBound: 11,
      });
      expect(pageWindow(15, 20, PAGES_WIDE)).toEqual({
        lowerBound: 11,
        upperBound: 20,
      });
      expect(pageWindow(16, 20, PAGES_WIDE)).toEqual({
        lowerBound: 11,
        upperBound: 20,
      });
      expect(pageWindow(4, 8, PAGES_WIDE)).toEqual({
        lowerBound: 1,
        upperBound: 8,
      });
    });

    it("keeps the current page in the middle of five", () => {
      for (let current = 1; current <= 20; current += 1) {
        const { lowerBound, upperBound } = pageWindow(current, 20, 5);
        expect(upperBound - lowerBound, `${current}`).toBe(4);
        expect(lowerBound).toBeGreaterThanOrEqual(1);
        expect(upperBound).toBeLessThanOrEqual(20);
        if (current >= 3 && current <= 18) expect(lowerBound).toBe(current - 2);
      }
    });
  });
});
