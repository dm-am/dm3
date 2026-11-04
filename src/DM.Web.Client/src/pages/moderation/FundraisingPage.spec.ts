/**
 * @vitest-environment jsdom
 */

// The button is the only place a save can be read: "Сохранено" only while the
// form matches what the server stored, never before a save or after a refusal,
// and gone on the first keystroke. A refusal stands in the form.
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  type VueWrapper,
} from "@vue/test-utils";
import Button from "@/shared/ui/Button/Button.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import FundraisingPage from "./FundraisingPage.vue";

const getFundraising = vi.hoisted(() => vi.fn());
const updateFundraising = vi.hoisted(() => vi.fn());

vi.mock("@/entities/fundraising", () => ({
  fundraisingApi: { getFundraising, updateFundraising },
}));

const goal = (over: Record<string, unknown> = {}) => ({
  title: "На сервер",
  goalAmount: 30000,
  collectedAmount: 12000,
  modifiedUtc: "2026-09-01T10:00:00Z",
  ...over,
});

const served = (resource: unknown) => ({ data: { resource }, error: null });

enableAutoUnmount(afterEach);

/** The real Button: a caption and a disabled attribute are what is checked. */
function render() {
  return mount(FundraisingPage, {
    global: {
      components: { Button },
      stubs: { SecondaryText: true },
    },
  });
}

async function rendered() {
  getFundraising.mockResolvedValue(served(goal()));
  const wrapper = render();
  await flushPromises();
  return wrapper;
}

const saveButton = (wrapper: VueWrapper) => wrapper.get(".form-actions button");

/** What the corner of the screen says, if anything. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

/** A save that goes through, answering with what it stored. */
async function save(wrapper: VueWrapper, stored = goal()) {
  updateFundraising.mockResolvedValue(served(stored));
  await wrapper.get("form").trigger("submit");
  await flushPromises();
}

describe("the fundraising form", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    for (const toast of [...toasts.value]) dismiss(toast.id);
    vi.clearAllMocks();
  });

  it("offers a save while nothing has been saved yet", async () => {
    const wrapper = await rendered();

    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();
  });

  it("says the save worked on the button and nowhere else", async () => {
    const wrapper = await rendered();

    await wrapper.get("#fundraising-collected").setValue(15000);
    await save(wrapper, goal({ collectedAmount: 15000 }));

    expect(updateFundraising).toHaveBeenCalledWith({
      title: "На сервер",
      goalAmount: 30000,
      collectedAmount: 15000,
    });
    expect(saveButton(wrapper).text()).toBe("Сохранено");
    expect(saveButton(wrapper).attributes("disabled")).toBeDefined();
    expect(plates()).toEqual([]);
  });

  it("offers the save again as soon as a field changes", async () => {
    const wrapper = await rendered();
    await wrapper.get("#fundraising-collected").setValue(15000);
    await save(wrapper, goal({ collectedAmount: 15000 }));

    await wrapper.get("#fundraising-title").setValue("На сервер и домен");

    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();
  });

  // The title goes as typed, so a trailing space is a change and is sent.
  it("sends a trailing space of the title as typed", async () => {
    const wrapper = await rendered();
    await save(wrapper);

    await wrapper.get("#fundraising-title").setValue("На сервер ");
    expect(saveButton(wrapper).text()).toBe("Сохранить");
    await save(wrapper);

    expect(updateFundraising.mock.calls.at(-1)?.[0]).toMatchObject({
      title: "На сервер ",
    });
  });

  // The one status the page owns: the server names the field and the rule.
  it("answers a rejected form inside the form, in the server's words", async () => {
    const wrapper = await rendered();
    updateFundraising.mockResolvedValue({
      data: null,
      error: {
        status: 400,
        title: "Ошибка валидации",
        errors: { title: ["Empty"] },
      },
    });

    await wrapper.get("#fundraising-title").setValue("   ");
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    const refusal = wrapper.get(".fundraising-form .form-error");
    expect(refusal.text()).toBe("Обязательное поле");
    // Neither the fallback nor the server's spelling of the field.
    expect(refusal.text()).not.toContain("title");
    expect(plates()).toEqual([]);
    expect(refusal.attributes("role")).toBe("alert");

    // The refusal must not go into `loadError`, which replaces the form.
    expect(wrapper.find(".fundraising-form").exists()).toBe(true);
    expect(
      (wrapper.get("#fundraising-title").element as HTMLInputElement).value,
    ).toBe("   ");
    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();
  });

  // The api is mocked here, so the test reads that the page itself says nothing.
  it("says a failure of status 500 in the form", async () => {
    const wrapper = await rendered();
    updateFundraising.mockResolvedValue({
      data: null,
      error: { status: 500, title: "Ошибка сервера" },
    });

    await wrapper.get("#fundraising-collected").setValue(15000);
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(wrapper.get(".form-error").text()).toBe("Ошибка сервера");
    expect(plates()).toEqual([]);
    expect(saveButton(wrapper).text()).toBe("Сохранить");
  });

  it.each([0, 401])(
    "leaves a failure of status %i to the interceptor",
    async (status) => {
      const wrapper = await rendered();
      updateFundraising.mockResolvedValue({
        data: null,
        error: { status, title: "Ошибка сервера" },
      });

      await wrapper.get("#fundraising-collected").setValue(15000);
      await wrapper.get("form").trigger("submit");
      await flushPromises();

      expect(wrapper.find(".form-error").exists()).toBe(false);
      expect(plates()).toEqual([]);
      // The save never happened, so the button still owes it.
      expect(saveButton(wrapper).text()).toBe("Сохранить");
      expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();
    },
  );

  it("takes the refusal off the screen on the first correction", async () => {
    const wrapper = await rendered();
    updateFundraising.mockResolvedValue({
      data: null,
      error: { status: 400, title: "Ошибка валидации" },
    });
    await wrapper.get("#fundraising-title").setValue("   ");
    await wrapper.get("form").trigger("submit");
    await flushPromises();
    expect(wrapper.find(".form-error").exists()).toBe(true);

    await wrapper.get("#fundraising-title").setValue("На сервер и домен");
    await flushPromises();

    // A sentence left over a changed field reads as a verdict on it.
    expect(wrapper.find(".form-error").exists()).toBe(false);
  });
});

// The form's twin while it is read, and the page's own line with a retry where it was refused.
describe("the read of the fundraising form", () => {
  beforeEach(() => vi.clearAllMocks());

  it("draws the form's skeleton in the form's frame until the goal arrives", async () => {
    let answer: (value: unknown) => void = () => {};
    getFundraising.mockReturnValue(
      new Promise((resolve) => (answer = resolve)),
    );
    const wrapper = render();

    const twin = wrapper.get(".fundraising-form");
    expect(twin.attributes("aria-hidden")).toBe("true");
    expect(twin.findAll(".form-field")).toHaveLength(3);
    expect(wrapper.text()).not.toContain("Загрузка...");
    expect(wrapper.find("form").exists()).toBe(false);

    answer(served(goal()));
    await flushPromises();
    expect(wrapper.find("form.fundraising-form").exists()).toBe(true);
  });

  it("says a refused read with a retry that reads the goal again", async () => {
    getFundraising
      .mockResolvedValueOnce({ data: null, error: { status: 500 } })
      .mockResolvedValueOnce(served(goal()));
    const wrapper = render();
    await flushPromises();

    expect(wrapper.get(".error-message").text()).toBe(
      "Не удалось загрузить данные сбора средств",
    );
    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(getFundraising).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".error-state").exists()).toBe(false);
    expect(wrapper.find("form").exists()).toBe(true);
  });
});
