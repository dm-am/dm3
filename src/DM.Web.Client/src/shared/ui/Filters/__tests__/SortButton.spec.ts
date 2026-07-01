/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import SortButton from "../controls/SortButton.vue";

// Mock SvgIcon
const SvgIconStub = {
  template: '<span class="svg-icon-stub" :data-name="name" />',
  props: ["name"],
};

describe("SortButton", () => {
  const defaultOptions = [
    {
      value: "created",
      label: "Дата",
      hint: "По дате создания",
      defaultDirection: "desc" as const,
    },
    {
      value: "name",
      label: "Название",
      hint: "По алфавиту",
      defaultDirection: "asc" as const,
    },
    { value: "rating", label: "Рейтинг", defaultDirection: "desc" as const },
  ];

  const mountComponent = (props = {}) => {
    return mount(SortButton, {
      props: {
        options: defaultOptions,
        sortBy: "created",
        sortOrder: "desc" as const,
        ...props,
      },
      global: {
        stubs: {
          SvgIcon: SvgIconStub,
        },
      },
    });
  };

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders sort button", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".sort-btn").exists()).toBe(true);
    });

    it("shows current sort label", () => {
      const wrapper = mountComponent({ sortBy: "name" });
      expect(wrapper.find(".sort-btn").text()).toContain("Название");
    });

    it("shows fallback label when sort not found", () => {
      const wrapper = mountComponent({ sortBy: "unknown" });
      expect(wrapper.find(".sort-btn").text()).toContain("Сортировка");
    });

    it("hides dropdown by default", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".sort-dropdown").exists()).toBe(false);
    });
  });

  // ============================================================================
  // DROPDOWN TOGGLE
  // ============================================================================

  describe("Dropdown Toggle", () => {
    it("shows dropdown on button click", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".sort-btn").trigger("click");
      expect(wrapper.find(".sort-dropdown").exists()).toBe(true);
    });

    it("hides dropdown on second click", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".sort-btn").trigger("click");
      await wrapper.find(".sort-btn").trigger("click");
      expect(wrapper.find(".sort-dropdown").exists()).toBe(false);
    });

    it("adds active class when dropdown is open", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".sort-btn").trigger("click");
      expect(wrapper.find(".sort-btn").classes()).toContain("active");
    });
  });

  // ============================================================================
  // SORT OPTIONS
  // ============================================================================

  describe("Sort Options", () => {
    it("renders all sort options", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".sort-btn").trigger("click");

      const options = wrapper.findAll(".sort-option:not(.sort-direction)");
      expect(options.length).toBe(3);
    });

    it("shows option labels", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".sort-btn").trigger("click");

      expect(wrapper.text()).toContain("Дата");
      expect(wrapper.text()).toContain("Название");
      expect(wrapper.text()).toContain("Рейтинг");
    });

    it("shows option hints", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".sort-btn").trigger("click");

      expect(wrapper.text()).toContain("По дате создания");
      expect(wrapper.text()).toContain("По алфавиту");
    });

    it("marks selected option", async () => {
      const wrapper = mountComponent({ sortBy: "created" });
      await wrapper.find(".sort-btn").trigger("click");

      const selectedOption = wrapper.find(".sort-option.selected");
      expect(selectedOption.text()).toContain("Дата");
    });

    it("emits update:sortBy when option clicked", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".sort-btn").trigger("click");

      const nameOption = wrapper.findAll(
        ".sort-option:not(.sort-direction)",
      )[1];
      await nameOption.trigger("click");

      expect(wrapper.emitted("update:sortBy")).toBeTruthy();
      expect(wrapper.emitted("update:sortBy")![0]).toEqual(["name"]);
    });

    it("emits default direction when option has one", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".sort-btn").trigger("click");

      const nameOption = wrapper.findAll(
        ".sort-option:not(.sort-direction)",
      )[1];
      await nameOption.trigger("click");

      expect(wrapper.emitted("update:sortOrder")).toBeTruthy();
      expect(wrapper.emitted("update:sortOrder")![0]).toEqual(["asc"]);
    });

    it("closes dropdown after selection", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".sort-btn").trigger("click");

      const option = wrapper.find(".sort-option:not(.sort-direction)");
      await option.trigger("click");

      expect(wrapper.find(".sort-dropdown").exists()).toBe(false);
    });
  });

  // ============================================================================
  // SORT DIRECTION
  // ============================================================================

  describe("Sort Direction", () => {
    it("renders direction toggle", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".sort-btn").trigger("click");

      expect(wrapper.find(".sort-direction").exists()).toBe(true);
    });

    it("shows current direction text (desc)", async () => {
      const wrapper = mountComponent({ sortOrder: "desc" });
      await wrapper.find(".sort-btn").trigger("click");

      expect(wrapper.find(".sort-direction").text()).toContain("По убыванию");
    });

    it("shows current direction text (asc)", async () => {
      const wrapper = mountComponent({ sortOrder: "asc" });
      await wrapper.find(".sort-btn").trigger("click");

      expect(wrapper.find(".sort-direction").text()).toContain(
        "По возрастанию",
      );
    });

    it("emits toggled direction on click", async () => {
      const wrapper = mountComponent({ sortOrder: "desc" });
      await wrapper.find(".sort-btn").trigger("click");
      await wrapper.find(".sort-direction").trigger("click");

      expect(wrapper.emitted("update:sortOrder")).toBeTruthy();
      expect(wrapper.emitted("update:sortOrder")![0]).toEqual(["asc"]);
    });
  });
});
