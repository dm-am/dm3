/**
 * @vitest-environment jsdom
 */

/**
 * The search row of a feed: the search of the chat and "Перейти к дате", both
 * whole at every width. Where the field beside the button would be narrower
 * than 160, the least a query has room in, the button stands under the field,
 * at its start: kept to one line, the field of a correspondence beside a wide
 * list went down to 80 and cut its own placeholder.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, enableAutoUnmount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { useAuthStore } from "@/shared/stores";
import type { User } from "@/shared/api/models/common";
import ChatSearchRow from "./ChatSearchRow.vue";

vi.mock("@/features/message-search/api/messageSearchApi", () => ({
  default: { searchMessages: vi.fn() },
}));

enableAutoUnmount(afterEach);

/** How many observers of a size were made: the row measures nothing. */
let observers = 0;

beforeEach(() => {
  setActivePinia(createPinia());
  useAuthStore().user = { username: "kot" } as unknown as User;
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

const HERE = dirname(fileURLToPath(import.meta.url));

/** The declarations of a component's rules on a part of it, by its class. */
function ruleOn(part: string, file = "ChatSearchRow.vue") {
  const rules = styleRules(componentCss(resolve(HERE, file)));
  return Object.assign(
    {},
    ...rules
      .filter((rule) => rule.selector.includes(part))
      .map((rule) => rule.declarations),
  ) as Record<string, string>;
}

describe("the row above a feed", () => {
  it("says the date in words, and measures nothing to decide it", () => {
    const wrapper = mountRow();
    expect(dateButton(wrapper).text()).toBe("Перейти к дате");
    expect(dateButton(wrapper).classes()).not.toContain("date-btn--icon");
    expect(observers).toBe(0);
  });

  it("puts the button under the field where the field would be narrower than 160", () => {
    // The field takes at least 160 beside the button: its frame, the
    // magnifier and room for a query, not only for its own words. A row with
    // less room for it beside the button breaks between the two, and only
    // there.
    expect(ruleOn(".filter-bar")["flex-wrap"]).toBe("wrap");
    expect(ruleOn(".search-container").flex).toBe("1 1 160px");
  });

  it("is the box the calendar of the date hangs from", () => {
    // The calendar hangs from the row, under it at its right edge
    // (ChatDateJump): the search's own box, whatever the bar's mixin says.
    expect(
      ruleOn(
        ".chat-search",
        "../../features/message-search/ui/MessageSearchBar.vue",
      ),
    ).toMatchObject({ position: "relative" });
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
