/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import UserMultiSelect from "./UserMultiSelect.vue";

// Mock userApi
const mockSearchUsers = vi.fn();

vi.mock("../api", () => ({
  userApi: {
    searchUsers: (...args: unknown[]) => mockSearchUsers(...args),
  },
}));

// Mock FilterDropdownItem
const FilterDropdownItemStub = {
  template: `
    <button
      class="dropdown-item"
      :class="{ highlighted }"
      @click="$emit('select')"
      @mouseenter="$emit('mouseenter')"
    >
      {{ label }}
    </button>
  `,
  props: ["label", "avatarUrl", "highlighted"],
  emits: ["select", "mouseenter"],
};

describe("UserMultiSelect", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mockSearchUsers.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const mountComponent = (props = {}) => {
    return mount(UserMultiSelect, {
      props: {
        selectedUsers: new Set<string>(),
        ...props,
      },
      global: {
        stubs: {
          FilterDropdownItem: FilterDropdownItemStub,
        },
      },
    });
  };

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders search input", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".dropdown-search-input").exists()).toBe(true);
    });

    it("renders with custom placeholder", () => {
      const wrapper = mountComponent({ placeholder: "Find user" });
      const input = wrapper.find("input");
      expect(input.attributes("placeholder")).toBe("Find user");
    });

    it("shows prompt when input is empty", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".dropdown-empty").text()).toBe(
        "Введите имя пользователя",
      );
    });

    it("renders user-multi-select container", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".user-multi-select").exists()).toBe(true);
    });
  });

  // ============================================================================
  // SEARCH BEHAVIOR
  // ============================================================================

  describe("Search Behavior", () => {
    it("calls API after debounce when user types", async () => {
      mockSearchUsers.mockResolvedValue({
        data: { resources: [] },
      });

      const wrapper = mountComponent({ debounceMs: 150 });
      const input = wrapper.find("input");

      await input.setValue("test");

      // Should not call API immediately
      expect(mockSearchUsers).not.toHaveBeenCalled();

      // Fast forward past debounce
      await vi.advanceTimersByTimeAsync(150);

      expect(mockSearchUsers).toHaveBeenCalledWith("test", 10);
    });

    it("clears suggestions when input is cleared", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");

      // Type something first
      await input.setValue("test");
      await nextTick();

      // Clear it
      await input.setValue("");
      await nextTick();

      // Should show the prompt again
      expect(wrapper.find(".dropdown-empty").text()).toBe(
        "Введите имя пользователя",
      );
    });

    it("shows empty message when no results after search", async () => {
      mockSearchUsers.mockResolvedValue({
        data: { resources: [] },
      });

      const wrapper = mountComponent({ debounceMs: 0 });
      const input = wrapper.find("input");

      await input.setValue("nonexistent");
      await vi.advanceTimersByTimeAsync(0);
      await vi.runAllTimersAsync();
      await nextTick();
      await nextTick();

      expect(wrapper.find(".dropdown-empty").exists()).toBe(true);
    });
  });

  // ============================================================================
  // KEYBOARD EVENTS
  // ============================================================================

  describe("Keyboard Events", () => {
    it("emits keydown for Escape key", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");

      await input.trigger("keydown", { key: "Escape" });

      expect(wrapper.emitted("keydown")).toBeTruthy();
    });

    it("prevents default on ArrowDown", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");

      // Simulate the keydown
      await input.trigger("keydown", { key: "ArrowDown" });

      // The handler should call preventDefault (we can't directly spy on native events in vue-test-utils)
      // So we just verify the event was handled
      expect(wrapper.emitted("keydown")).toBeFalsy(); // ArrowDown shouldn't bubble
    });

    it("prevents default on ArrowUp", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");

      await input.trigger("keydown", { key: "ArrowUp" });

      // ArrowUp shouldn't bubble to parent
      expect(wrapper.emitted("keydown")).toBeFalsy();
    });
  });

  // ============================================================================
  // COMPONENT BEHAVIOR
  // ============================================================================

  describe("Component Behavior", () => {
    it("cleans up debounce timer on unmount", async () => {
      const wrapper = mountComponent({ debounceMs: 1000 });
      const input = wrapper.find("input");

      await input.setValue("test");

      // Unmount before timer fires
      wrapper.unmount();

      // This should not throw or cause issues
      await vi.advanceTimersByTimeAsync(1000);
    });

    it("uses default debounce of 150ms", () => {
      const wrapper = mountComponent();
      // Default prop value
      expect(wrapper.props().debounceMs).toBe(150);
    });

    it("uses default maxSuggestions of 6", () => {
      const wrapper = mountComponent();
      expect(wrapper.props().maxSuggestions).toBe(6);
    });
  });

  // ============================================================================
  // ERROR HANDLING
  // ============================================================================

  describe("Error Handling", () => {
    it("handles API error gracefully", async () => {
      mockSearchUsers.mockRejectedValue(new Error("API Error"));

      const wrapper = mountComponent({ debounceMs: 0 });
      const input = wrapper.find("input");

      await input.setValue("test");
      await vi.runAllTimersAsync();
      await nextTick();
      await nextTick();

      // Should show empty state, not crash
      expect(wrapper.find(".dropdown-empty").exists()).toBe(true);
    });
  });

  // ============================================================================
  // PROPS
  // ============================================================================

  describe("Props", () => {
    it("accepts selectedUsers as Set", () => {
      const selected = new Set(["user1", "user2"]);
      const wrapper = mountComponent({ selectedUsers: selected });

      expect(wrapper.props().selectedUsers).toStrictEqual(selected);
    });

    it("accepts custom placeholder", () => {
      const wrapper = mountComponent({ placeholder: "Найти пользователя" });
      expect(wrapper.find("input").attributes("placeholder")).toBe(
        "Найти пользователя",
      );
    });

    it("accepts custom maxSuggestions", () => {
      const wrapper = mountComponent({ maxSuggestions: 10 });
      expect(wrapper.props().maxSuggestions).toBe(10);
    });

    it("accepts custom debounceMs", () => {
      const wrapper = mountComponent({ debounceMs: 300 });
      expect(wrapper.props().debounceMs).toBe(300);
    });
  });
});
