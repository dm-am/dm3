/**
 * @vitest-environment jsdom
 */

/**
 * The birthday field of the profile editor, as it is wired to the page.
 *
 * The date goes to the page's edit draft (useProfileEdit) and is saved with
 * "Завершить"; until then the profile handed down to this block is the saved
 * one. Bound to the saved date alone, the field snapped back to it the moment
 * it lost focus, so a birthday picked or typed seemed to vanish although the
 * draft held it.
 *
 * The field also takes no day after today, opens an empty value on the grid
 * of years round the current year less twelve, stands as wide as its
 * calendar, and says under it why it does not take a date typed in.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import ProfilePersonalInfo from "./ProfilePersonalInfo.vue";
import { DateInput } from "@/shared/ui/DatePicker";
import type { User } from "@/shared/api/models/community";

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date(2026, 8, 28, 12, 0));
});

afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = "";
});

const mountInfo = (user: Record<string, unknown> = {}) =>
  mount(ProfilePersonalInfo, {
    props: {
      user: { username: "Reader", contacts: [], ...user } as unknown as User,
      isEditMode: true,
    },
    attachTo: document.body,
  });

const birthdayInput = (wrapper: ReturnType<typeof mountInfo>) =>
  wrapper.find('input[aria-label="День рождения"]');

describe("the birthday field of the profile editor", () => {
  it("keeps a typed date on screen after it loses focus", async () => {
    const wrapper = mountInfo();
    const input = birthdayInput(wrapper);
    await input.setValue("14031991");
    await input.trigger("blur");
    expect((input.element as HTMLInputElement).value).toBe("14.03.1991");
    expect(wrapper.emitted("updateField")?.at(-1)).toEqual([
      "birthday",
      "1991-03-14",
    ]);
  });

  it("forgets the unsaved date when editing ends", async () => {
    const wrapper = mountInfo({
      birthday: { day: 1, month: 2, year: 1990 },
    });
    await birthdayInput(wrapper).setValue("14.03.1991");
    await wrapper.setProps({ isEditMode: false });
    await wrapper.setProps({ isEditMode: true });
    expect((birthdayInput(wrapper).element as HTMLInputElement).value).toBe(
      "01.02.1990",
    );
  });

  it("takes no day after today, opens empty on the years and is narrow", () => {
    const wrapper = mountInfo();
    const field = wrapper.findComponent(DateInput);
    expect(field.props("max")).toBe("2026-09-28");
    expect(field.props("emptyLevel")).toBe("years");
    expect(field.props("narrow")).toBe(true);
    // 2003-2014, not 2015-2026: the last twelve years hold nobody's birthday.
    expect(field.props("openOn")).toBe("2014-09-28");
  });

  it("says under the field why it does not take a date typed in", async () => {
    const wrapper = mountInfo();
    const input = birthdayInput(wrapper);
    await input.setValue("31.02.1991");
    await input.trigger("blur");

    expect((input.element as HTMLInputElement).value).toBe("31.02.1991");
    expect(wrapper.get(".form-field-error").text()).toBe("Такой даты нет");
    expect(input.attributes("aria-invalid")).toBe("true");
    expect(wrapper.emitted("updateField")).toBeUndefined();

    await input.setValue("01.10.2026");
    await input.trigger("blur");
    expect(wrapper.get(".form-field-error").text()).toBe("Не позже 28.09.2026");

    await input.setValue("14.03.1991");
    expect(wrapper.find(".form-field-error").exists()).toBe(false);
  });

  it("forgets what it said when editing ends", async () => {
    const wrapper = mountInfo();
    await birthdayInput(wrapper).setValue("31.02.1991");
    await birthdayInput(wrapper).trigger("blur");
    await wrapper.setProps({ isEditMode: false });
    await wrapper.setProps({ isEditMode: true });
    expect(wrapper.find(".form-field-error").exists()).toBe(false);
  });
});
