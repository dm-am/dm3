/**
 * @vitest-environment jsdom
 */

/**
 * A date and a time side by side, in place of <input type="datetime-local">,
 * with the same value the browser's field gave ("YYYY-MM-DDTHH:mm"), and one
 * reason for the row: the date's, the time's or the bounds'.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import DateTimeInput from "./DateTimeInput.vue";

// "Today" for the calendar: 6 October 2026.
beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date(2026, 9, 6, 12, 0));
});

afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = "";
});

const mountRow = (props: Record<string, unknown> = {}) =>
  mount(DateTimeInput, {
    props: { modelValue: "2026-10-06T05:30", ...props },
    attachTo: document.body,
  });

const dateOf = (wrapper: ReturnType<typeof mountRow>) =>
  wrapper.find(".dti-date .di-input");
const timeOf = (wrapper: ReturnType<typeof mountRow>) =>
  wrapper.find("input.dti-time");
const shown = (wrapper: ReturnType<typeof mountRow>) =>
  [dateOf(wrapper), timeOf(wrapper)]
    .map((one) => (one.element as HTMLInputElement).value)
    .join(" ");

const lastReason = (wrapper: ReturnType<typeof mountRow>) =>
  wrapper.emitted("reason")?.at(-1)?.[0] ?? null;

describe("DateTimeInput", () => {
  it("shows the moment as a date and a time, with no field of the browser", () => {
    const wrapper = mountRow();
    expect(shown(wrapper)).toBe("06.10.2026 05:30");
    expect(wrapper.find('input[type="datetime-local"]').exists()).toBe(false);
    expect(dateOf(wrapper).attributes("aria-label")).toBe("Дата");
    expect(timeOf(wrapper).attributes("aria-label")).toBe("Время");
  });

  it("has no cross: the date must be given", () => {
    expect(mountRow().find(".di-clear").exists()).toBe(false);
  });

  it("gives the date field the id a caption points at", () => {
    expect(dateOf(mountRow({ id: "poll-starts" })).attributes("id")).toBe(
      "poll-starts",
    );
  });

  it("joins a new time to the date", async () => {
    const wrapper = mountRow();
    await timeOf(wrapper).setValue("2000");
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
      "2026-10-06T20:00",
    ]);
  });

  it("joins a new date to the time", async () => {
    const wrapper = mountRow();
    await dateOf(wrapper).setValue("09.10.2026");
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
      "2026-10-09T05:30",
    ]);
  });

  it("is no moment while a part is empty, and keeps the other part", async () => {
    const wrapper = mountRow({
      "onUpdate:modelValue": (value: string) =>
        wrapper.setProps({ modelValue: value }),
    });
    await timeOf(wrapper).setValue("");
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([""]);
    expect(shown(wrapper)).toBe("06.10.2026 ");
  });

  it("shows a moment set from outside", async () => {
    const wrapper = mountRow();
    await wrapper.setProps({ modelValue: "2027-01-02T09:15" });
    expect(shown(wrapper)).toBe("02.01.2027 09:15");
  });

  it("says why the time is not one", async () => {
    const wrapper = mountRow();
    await timeOf(wrapper).setValue("2500");
    await timeOf(wrapper).trigger("blur");
    expect(lastReason(wrapper)).toBe("Такого времени нет");
  });

  it("says why the date is not one", async () => {
    const wrapper = mountRow();
    await dateOf(wrapper).setValue("31.02.2026");
    await dateOf(wrapper).trigger("blur");
    expect(lastReason(wrapper)).toBe("Такой даты нет");
  });

  describe("bounds", () => {
    it("takes no day before the day of the earliest moment", async () => {
      const wrapper = mountRow({ min: "2026-10-06T05:30" });
      await dateOf(wrapper).setValue("05.10.2026");
      await dateOf(wrapper).trigger("blur");
      expect(lastReason(wrapper)).toBe("Не раньше 06.10.2026");
      expect(wrapper.emitted("update:modelValue")).toBeUndefined();
    });

    it("takes no time before it on its own day, and names the moment", async () => {
      const wrapper = mountRow({ min: "2026-10-06T05:30" });
      await timeOf(wrapper).setValue("0500");
      expect(lastReason(wrapper)).toBe("Не раньше 06.10.2026 05:30");
      expect(wrapper.emitted("update:modelValue")).toBeUndefined();
      await timeOf(wrapper).setValue("0545");
      expect(lastReason(wrapper)).toBeNull();
      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
        "2026-10-06T05:45",
      ]);
    });

    it("takes the same time on a later day", async () => {
      const wrapper = mountRow({
        modelValue: "2026-10-07T05:00",
        min: "2026-10-06T05:30",
      });
      expect(lastReason(wrapper)).toBeNull();
    });

    it("takes no moment after the latest", async () => {
      const wrapper = mountRow({ max: "2026-10-06T18:00" });
      await timeOf(wrapper).setValue("1801");
      expect(lastReason(wrapper)).toBe("Не позже 06.10.2026 18:00");
      expect(wrapper.emitted("update:modelValue")).toBeUndefined();
    });

    it("says so when the bounds move from under the moment", async () => {
      // The end of a poll whose start moved past it.
      const wrapper = mountRow({ min: "2026-10-01T00:00" });
      expect(lastReason(wrapper)).toBeNull();
      await wrapper.setProps({ min: "2026-10-06T12:00" });
      expect(lastReason(wrapper)).toBe("Не раньше 06.10.2026 12:00");
    });
  });
});
