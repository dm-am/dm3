/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount } from "@vue/test-utils";

import ChatDateJump from "./ChatDateJump.vue";

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

describe("the chat's date jump at any width", () => {
  it("says its words", () => {
    const wrapper = mountJump("", { narrow: true });
    expect(wrapper.find(".date-btn").text()).toBe("Перейти к дате");
  });
});
