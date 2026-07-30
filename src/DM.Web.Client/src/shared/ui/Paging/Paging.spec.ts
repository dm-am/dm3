/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import Paging from "./Paging.vue";
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
    size: 20,
    number: 5,
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
});
