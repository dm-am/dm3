/**
 * @vitest-environment jsdom
 */

/** The search row of a feed: the search of the chat and "Перейти к дате". */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, enableAutoUnmount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { createMockUser } from "@/entities/forum/model/store.fixtures";
import ChatSearchRow from "./ChatSearchRow.vue";

vi.mock("@/features/message-search/api/messageSearchApi", () => ({
  default: { searchMessages: vi.fn() },
}));

enableAutoUnmount(afterEach);

/** How many observers of a size were made: the row measures nothing. */
let observers = 0;

beforeEach(() => {
  setActivePinia(createPinia());
  useAuthStore().user = createMockUser("u-kot", "kot");
  observers = 0;
  vi.stubGlobal(
    "ResizeObserver",
    class {
      constructor() {
        observers += 1;
      }
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
  document.body.innerHTML = "";
});

const mountRow = (scope: string = "global") =>
  mount(ChatSearchRow, {
    props: { scope: scope as "global" },
    attachTo: document.body,
  });

const dateButton = (wrapper: ReturnType<typeof mountRow>) =>
  wrapper.find(".date-btn");

describe("the row above a feed", () => {
  it("says the date in words, and measures nothing to decide it", () => {
    const wrapper = mountRow();
    expect(dateButton(wrapper).text()).toBe("Перейти к дате");
    expect(dateButton(wrapper).classes()).not.toContain("date-btn--icon");
    expect(observers).toBe(0);
  });

  it("searches the feed it stands over", () => {
    const wrapper = mountRow("dm:c7");
    expect(wrapper.find("input").attributes("placeholder")).toBe(
      "Поиск по переписке",
    );
  });

  it("takes Ctrl+F to its field", () => {
    const wrapper = mountRow("dm:c7");
    const event = new KeyboardEvent("keydown", {
      key: "f",
      ctrlKey: true,
      cancelable: true,
    });
    document.dispatchEvent(event);
    expect(document.activeElement).toBe(wrapper.find("input").element);
    expect(event.defaultPrevented).toBe(true);
  });

  it("puts the caret in its field when the page asks, and says whether it could", () => {
    const wrapper = mountRow("dm:c7");
    expect((wrapper.vm as unknown as { focus: () => boolean }).focus()).toBe(
      true,
    );
    expect(document.activeElement).toBe(wrapper.find("input").element);
  });

  it("leaves Ctrl+F to the browser for a guest, who has no field", () => {
    useAuthStore().user = null;
    mountRow();
    const event = new KeyboardEvent("keydown", {
      key: "f",
      ctrlKey: true,
      cancelable: true,
    });
    document.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(false);
  });

  it("passes the opening of the calendar and a picked day on", async () => {
    const wrapper = mountRow();
    await dateButton(wrapper).trigger("click");
    expect(wrapper.emitted("date-open")).toHaveLength(1);

    await wrapper.find(".dp-grid").trigger("keydown", { key: "Enter" });
    expect(wrapper.emitted("date-picked")).toHaveLength(1);
  });
});
