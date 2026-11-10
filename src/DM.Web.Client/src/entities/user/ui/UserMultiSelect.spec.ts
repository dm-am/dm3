/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import UserMultiSelect from "./UserMultiSelect.vue";

const FEATURES = join(
  dirname(fileURLToPath(import.meta.url)),
  "../../../features",
);

const mockSearchUsers = vi.fn();

vi.mock("../api", () => ({
  userApi: {
    searchUsers: (...args: unknown[]) => mockSearchUsers(...args),
  },
}));

const FilterDropdownItemStub = {
  template: `
    <button
      class="dropdown-item"
      :class="{ highlighted }"
      @click="$emit('item-select')"
      @mouseenter="$emit('mouseenter')"
    >
      {{ label }}
    </button>
  `,
  props: ["label", "avatarUrl", "highlighted"],
  emits: ["item-select", "mouseenter"],
};

describe("UserMultiSelect", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mockSearchUsers.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const mountComponent = (props = {}) =>
    mount(UserMultiSelect, {
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

  describe("Search Behavior", () => {
    it("calls API after debounce when user types", async () => {
      mockSearchUsers.mockResolvedValue({
        data: { resources: [] },
      });

      const wrapper = mountComponent();
      const input = wrapper.find("input");

      await input.setValue("test");

      expect(mockSearchUsers).not.toHaveBeenCalled();

      await vi.advanceTimersByTimeAsync(150);

      expect(mockSearchUsers).toHaveBeenCalledWith("test", 10, undefined);
    });

    it("looks among everyone, the long absent too, when asked", async () => {
      mockSearchUsers.mockResolvedValue({ data: { resources: [] } });

      const wrapper = mountComponent({ includeInactive: true });
      await wrapper.find("input").setValue("test");
      await vi.advanceTimersByTimeAsync(150);

      expect(mockSearchUsers).toHaveBeenCalledWith("test", 10, "All");
    });

    it("clears suggestions when input is cleared", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");

      await input.setValue("test");
      await nextTick();

      await input.setValue("");
      await nextTick();

      expect(wrapper.find(".dropdown-empty").text()).toBe(
        "Введите имя пользователя",
      );
    });

    it("shows empty message when no results after search", async () => {
      mockSearchUsers.mockResolvedValue({
        data: { resources: [] },
      });

      const wrapper = mountComponent();
      const input = wrapper.find("input");

      await input.setValue("nonexistent");
      await vi.advanceTimersByTimeAsync(150);
      await vi.runAllTimersAsync();
      await nextTick();
      await nextTick();

      expect(wrapper.find(".dropdown-empty").text()).toBe(
        "Пользователь не найден",
      );
    });

    // A refused search is not an answer that nobody has the name.
    it("says the search failed when it was refused", async () => {
      mockSearchUsers.mockResolvedValue({
        data: null,
        error: { status: 500, title: "Ошибка" },
      });

      const wrapper = mountComponent();
      await wrapper.find("input").setValue("anna");
      await vi.advanceTimersByTimeAsync(150);
      await nextTick();

      expect(wrapper.find(".dropdown-empty").text()).toBe(
        "Не удалось загрузить пользователей",
      );
    });
  });

  describe("Keyboard Events", () => {
    it("emits keydown for Escape key", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");

      await input.trigger("keydown", { key: "Escape" });

      expect(wrapper.emitted("keydown")).toBeTruthy();
    });

    it.each(["ArrowDown", "ArrowUp"])("prevents default on %s", (key) => {
      const wrapper = mountComponent();
      const event = new KeyboardEvent("keydown", { key, cancelable: true });

      wrapper.find("input").element.dispatchEvent(event);

      expect(event.defaultPrevented).toBe(true);
      expect(wrapper.emitted("keydown")).toBeFalsy();
    });
  });

  describe("Component Behavior", () => {
    it("cleans up debounce timer on unmount", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");

      await input.setValue("test");

      wrapper.unmount();

      await vi.advanceTimersByTimeAsync(1000);
    });
  });
});

// A list holds the old as well, and the server finds only the active by default.
describe("the people of every filter of the site", () => {
  it("are looked for among everyone", () => {
    const pickers: string[] = [];
    const offenders: string[] = [];
    for (const file of readdirSync(FEATURES, {
      recursive: true,
      encoding: "utf8",
    })) {
      if (!file.endsWith(".vue")) continue;
      const source = readFileSync(join(FEATURES, file), "utf8");
      for (const [tag] of source.matchAll(/<UserMultiSelect\b[^>]*>/g)) {
        const name = file.split("\\").join("/");
        pickers.push(name);
        if (!/\sinclude-inactive\b/.test(tag)) offenders.push(name);
      }
    }
    expect(pickers.length).toBeGreaterThanOrEqual(6);
    expect(offenders).toEqual([]);
  });
});
