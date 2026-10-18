/**
 * @vitest-environment jsdom
 */

/**
 * "Перейти к дате" and its calendar, as the keyboard meets them.
 *
 * The calendar opened from the button takes the focus onto the day on view
 * (the one the chat is on, else today), so the arrows work at once; Escape
 * closes it and gives the focus back to the button, and so does a pick. Before,
 * the 42 days were 42 stops of Tab and a pick dropped the focus on the page.
 *
 * And what it offers: the days the feed has, from its first message to today,
 * and its words at every width, with the calendar hung from the row the
 * button stands in.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import ChatDateJump from "./ChatDateJump.vue";

/** The declarations of the control's rules on a part of it, by its class. */
function ruleOn(part: string) {
  const file = resolve(
    dirname(fileURLToPath(import.meta.url)),
    "ChatDateJump.vue",
  );
  return Object.assign(
    {},
    ...styleRules(componentCss(file))
      .filter((rule) => rule.selector.includes(part))
      .map((rule) => rule.declarations),
  ) as Record<string, string>;
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date(2026, 8, 28, 12, 0));
});

afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = "";
});

const mountJump = (
  selectedDate = "2026-09-15",
  extra: Record<string, unknown> = {},
) =>
  mount(ChatDateJump, {
    props: { selectedDate, maxDate: "2026-09-28", ...extra },
    attachTo: document.body,
  });

const open = async (wrapper: ReturnType<typeof mountJump>) => {
  await wrapper.find(".date-btn").trigger("click");
  await wrapper.vm.$nextTick();
  await wrapper.vm.$nextTick();
};

const focusedText = () =>
  (document.activeElement as HTMLElement | null)?.textContent?.trim();

/** The days of the month on view that can be picked, by their numbers. */
const pickable = (wrapper: ReturnType<typeof mountJump>) =>
  wrapper
    .findAll(".dp-day")
    .filter((day) => day.attributes("disabled") === undefined)
    .map((day) => Number(day.text()));

describe("the chat's date jump", () => {
  it("puts the focus on the day the chat is on when it opens", async () => {
    const wrapper = mountJump();
    await open(wrapper);
    expect(focusedText()).toBe("15");
  });

  it("puts it on today when the chat is on no day", async () => {
    const wrapper = mountJump("");
    await open(wrapper);
    expect(focusedText()).toBe("28");
  });

  it("gives the focus back to the button on Escape", async () => {
    const wrapper = mountJump();
    await open(wrapper);
    await wrapper.find(".dp-day.selected").trigger("keydown", {
      key: "Escape",
    });
    expect(wrapper.find(".calendar-grid").exists()).toBe(false);
    expect(document.activeElement).toBe(wrapper.find(".date-btn").element);
  });

  it("gives the focus back to the button on a pick", async () => {
    const wrapper = mountJump();
    await open(wrapper);
    await wrapper.find(".dp-grid").trigger("keydown", { key: "ArrowLeft" });
    await wrapper.find(".dp-grid").trigger("keydown", { key: "Enter" });
    expect(wrapper.emitted("date-picked")?.at(-1)).toEqual(["2026-09-14"]);
    expect(wrapper.find(".calendar-grid").exists()).toBe(false);
    expect(document.activeElement).toBe(wrapper.find(".date-btn").element);
  });
});

describe("the days the chat's date jump offers", () => {
  it("offers the days from the first message to today", async () => {
    const wrapper = mountJump("", { minDate: "2026-09-10" });
    await open(wrapper);
    const days = pickable(wrapper);
    expect(days[0]).toBe(10);
    expect(days.at(-1)).toBe(28);
    expect(days).toHaveLength(19);
  });

  it("offers every day up to today when the first one is not known", async () => {
    const wrapper = mountJump("");
    await open(wrapper);
    expect(pickable(wrapper)[0]).toBe(1);
  });

  it("says it opened, and only when it opens", async () => {
    const wrapper = mountJump();
    await open(wrapper);
    await wrapper.find(".date-btn").trigger("click");
    expect(wrapper.emitted("open")).toHaveLength(1);
  });
});

/**
 * A narrow row once had the button give up its words for its icon alone, a
 * square a phone showed in their place. The owner, 12 October 2026: nothing
 * is shortened on a phone. Nothing can ask for that form any more.
 */
describe("the chat's date jump at any width", () => {
  it("says its words, and has no form without them", () => {
    const wrapper = mountJump("", { narrow: true });
    const button = wrapper.find(".date-btn");
    expect(button.text()).toBe("Перейти к дате");
    expect(button.classes()).not.toContain("date-btn--icon");
    expect(wrapper.props()).not.toHaveProperty("narrow");
  });

  /**
   * Where the field leaves the button no room beside it, the button stands
   * under the field, at the start of the row (ChatSearchRow). A calendar hung
   * from the right edge of the button reached 111 past the row on the left,
   * over the column beside the chat (1001). Hung from the row, under it at its
   * right edge, it keeps within the row on one line and on two alike.
   */
  it("hangs its calendar from the row it stands in, not from the button", () => {
    expect(ruleOn(".date-section")).not.toHaveProperty("position");
    expect(ruleOn(".date-popover")).toMatchObject({
      position: "absolute",
      right: "0",
    });
  });
});
