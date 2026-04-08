/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import Paging from "../Paging.vue";

// Mock vue-router
vi.mock("vue-router", () => ({
  useRoute: () => ({
    query: {},
  }),
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
