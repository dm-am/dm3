/**
 * @vitest-environment jsdom
 */

/**
 * The dates of a new poll are the site's fields of a date and a time, and not
 * the browser's <input type="datetime-local">, whose look, language and "PM"
 * were the browser's. What the fields say is wrong with a date stands under
 * the field it is about.
 */
const MACHINE_ZONE = process.env.TZ;
process.env.TZ = "Europe/Moscow";

import {
  describe,
  it,
  expect,
  vi,
  beforeEach,
  afterEach,
  afterAll,
} from "vitest";
import { mount } from "@vue/test-utils";
import FormField from "@/shared/ui/Form/FormField.vue";
import CreatePollForm from "./CreatePollForm.vue";

const { createPoll } = vi.hoisted(() => ({ createPoll: vi.fn() }));

vi.mock("@/entities/poll", () => ({
  usePollsStore: () => ({ createPoll }),
}));

vi.mock("@/entities/user", () => ({
  useAuthStore: () => ({ user: { username: "Модератор" } }),
  userIsSeniorModerator: () => true,
}));

beforeEach(() => {
  createPoll.mockReset();
  vi.useFakeTimers({
    now: new Date("2026-10-06T02:30:00Z"),
    toFake: ["Date"],
  });
});

afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = "";
});

afterAll(() => {
  if (MACHINE_ZONE === undefined) delete process.env.TZ;
  else process.env.TZ = MACHINE_ZONE;
});

const render = () =>
  mount(CreatePollForm, {
    attachTo: document.body,
    global: { components: { FormField } },
  });

/** The field of a caption, and the date and the time inside it. */
const field = (wrapper: ReturnType<typeof render>, caption: string) => {
  const box = wrapper
    .findAll(".form-field")
    .find((one) => one.find(".form-field-label").text() === caption)!;
  return {
    box,
    date: box.find(".dti-date .di-input"),
    time: box.find("input.dti-time"),
  };
};

describe("the dates of a new poll", () => {
  it("are a date and a time of the site, not the browser's field", async () => {
    const wrapper = render();
    await wrapper.vm.$nextTick();
    expect(wrapper.find('input[type="datetime-local"]').exists()).toBe(false);

    const starts = field(wrapper, "Начало");
    expect((starts.date.element as HTMLInputElement).value).toBe("06.10.2026");
    expect((starts.time.element as HTMLInputElement).value).toBe("05:30");
    // Named by the caption as one group of two fields.
    expect(starts.box.find('[role="group"]').exists()).toBe(true);
  });

  it("say under the end that it comes too soon", async () => {
    const wrapper = render();
    const ends = field(wrapper, "Окончание");
    await ends.date.setValue("06.10.2026");
    await ends.date.trigger("blur");

    expect(ends.box.find(".form-field-error").text()).toBe(
      "Не раньше 07.10.2026",
    );
    expect(
      field(wrapper, "Начало").box.find(".form-field-error").exists(),
    ).toBe(false);
  });

  it("say under the start that it is gone by", async () => {
    const wrapper = render();
    const starts = field(wrapper, "Начало");
    await starts.time.setValue("0500");

    expect(starts.box.find(".form-field-error").text()).toBe(
      "Не раньше 06.10.2026 05:30",
    );
  });

  it("are not sent while a field says what is wrong with it", async () => {
    const wrapper = render();
    await wrapper.find("#poll-title").setValue("Любимый модуль года");
    const options = wrapper.findAll(".option-row input");
    await options[0].setValue("Первый");
    await options[1].setValue("Второй");
    await field(wrapper, "Начало").time.setValue("0500");

    const create = wrapper
      .findAll("button")
      .find((button) => button.text() === "Создать опрос")!;
    await create.trigger("click");

    expect(createPoll).not.toHaveBeenCalled();
    expect(wrapper.get(".error-message").text()).toBe(
      "Проверьте начало и окончание опроса",
    );
  });
});
