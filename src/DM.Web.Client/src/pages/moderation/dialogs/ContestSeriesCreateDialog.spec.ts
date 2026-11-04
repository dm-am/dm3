/**
 * @vitest-environment jsdom
 */

// The window runs the request: a refusal, above all a series already there,
// is printed in it and it stays open. The shared Select is a native list here.
import { afterEach, describe, it, expect, vi, beforeEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { ContestType } from "@/shared/api/models/achievements";
import { setScrollContainer } from "@/shared/lib/scroll";
import ContestSeriesCreateDialog from "./ContestSeriesCreateDialog.vue";

const createContestSeries = vi.hoisted(() => vi.fn());

vi.mock("@/entities/achievement", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/achievement")>()),
  achievementApi: { createContestSeries },
}));

vi.mock("vue-final-modal", async () =>
  (await import("@/shared/lib/testing/vueFinalModalStub")).vueFinalModal(),
);

const FLAGS = { ownsRefusal: true, ownsRateLimit: true };

enableAutoUnmount(afterEach);

afterEach(() => {
  document.body.innerHTML = "";
  setScrollContainer(null);
});

/** The window as useModal opens it, with the library's closing recorded. */
function render() {
  const shut = vi.fn();
  const wrapper = mount(ContestSeriesCreateDialog, {
    attachTo: document.body,
    props: {
      series: [],
      modelValue: true,
      "onUpdate:modelValue": shut,
    } as never,
    global: {
      stubs: {
        Select: {
          props: ["modelValue", "options", "id"],
          emits: ["update:modelValue"],
          template: `<select :id="id" class='select-stub' :value="modelValue"
            @change="$emit('update:modelValue', $event.target.value)">
            <option v-for="o in options" :key="o.value" :value="o.value">{{ o.label }}</option>
          </select>`,
        },
      },
    },
  });
  return { wrapper, shut };
}

async function create(wrapper: ReturnType<typeof render>["wrapper"]) {
  await wrapper.get("form").trigger("submit");
  await flushPromises();
}

const refused = (status: number, title = "Ошибка сервера") => ({
  data: null,
  error: { type: "Failure", status, title, traceId: "t" },
});

describe("the contest series dialog", () => {
  beforeEach(() => {
    createContestSeries.mockReset();
    createContestSeries.mockResolvedValue({ data: null, error: null });
  });

  it("creates the series the form describes, with the window's flags", async () => {
    const { wrapper, shut } = render();
    await create(wrapper);

    expect(createContestSeries).toHaveBeenCalledWith(
      {
        contestType: ContestType.Literary,
        number: 1,
        year: new Date().getFullYear(),
        topicUrl: null,
      },
      FLAGS,
    );
    expect(wrapper.emitted("success")).toBeTruthy();
    expect(shut).toHaveBeenCalledWith(false);
    expect(wrapper.find(".form-refusal").exists()).toBe(false);
  });

  /** A series already there, and the refusals the window takes over from the interceptor. */
  it.each([
    [409, "Такая серия уже существует"],
    [403, "Недостаточно прав"],
    [503, "Ошибка сервера"],
  ])(
    "prints a refusal of status %i in the window and stays open",
    async (status, title) => {
      createContestSeries.mockResolvedValue(refused(status, title));
      const { wrapper, shut } = render();
      await create(wrapper);

      expect(wrapper.get(".form-refusal").text()).toBe(title);
      expect(wrapper.emitted("success")).toBeFalsy();
      expect(shut).not.toHaveBeenCalled();
      expect(
        (wrapper.get("#series-number").element as HTMLInputElement).value,
      ).toBe("1");
    },
  );

  it("says its own sentence when the server named nothing", async () => {
    createContestSeries.mockResolvedValue(refused(404, ""));
    const { wrapper } = render();
    await create(wrapper);

    expect(wrapper.get(".form-refusal").text()).toBe(
      "Не удалось создать серию",
    );
  });
});

// The year is the site's field of four digits, not the browser's number field.
describe("the year of a contest series", () => {
  beforeEach(() => {
    createContestSeries.mockReset();
    createContestSeries.mockResolvedValue({ data: null, error: null });
  });

  const year = (wrapper: ReturnType<typeof render>["wrapper"]) =>
    wrapper.get("#series-year");
  const nextYear = new Date().getFullYear() + 1;
  const createButton = (wrapper: ReturnType<typeof render>["wrapper"]) =>
    wrapper.findAll("button").find((button) => button.text() === "Создать")!;

  it("is a field of four digits and not a number field", () => {
    const { wrapper } = render();
    expect(year(wrapper).attributes("type")).toBe("text");
    expect(year(wrapper).attributes("maxlength")).toBe("4");
    expect((year(wrapper).element as HTMLInputElement).value).toBe(
      String(new Date().getFullYear()),
    );
    expect(wrapper.find('#series-year[type="number"]').exists()).toBe(false);
  });

  it("says under the field why it does not take a year, and shuts the button", async () => {
    const { wrapper } = render();
    await year(wrapper).setValue("202");
    await year(wrapper).trigger("blur");

    const field = year(wrapper).element.closest(".form-field")!;
    expect(field.querySelector(".form-field-error")?.textContent?.trim()).toBe(
      "Год из четырех цифр",
    );
    expect(createButton(wrapper).attributes("disabled")).toBeDefined();
  });

  // The server's bounds: the opening of the site and a year past the current one.
  it("names the bounds", async () => {
    const { wrapper } = render();
    await year(wrapper).setValue("2006");
    await year(wrapper).trigger("blur");
    expect(wrapper.get(".form-field-error").text()).toBe("Не раньше 2007");

    await year(wrapper).setValue(String(nextYear + 1));
    await year(wrapper).trigger("blur");
    expect(wrapper.get(".form-field-error").text()).toBe(
      `Не позже ${nextYear}`,
    );
  });

  it("takes the opening year of the site and the next one", async () => {
    const { wrapper } = render();
    for (const taken of ["2007", String(nextYear)]) {
      await year(wrapper).setValue(taken);
      await year(wrapper).trigger("blur");
      expect(wrapper.find(".form-field-error").exists()).toBe(false);
    }
  });

  // Enter sends a form by the browser's default action; cancelling the key cancels it.
  const enter = (target: Element) => {
    const press = new KeyboardEvent("keydown", {
      key: "Enter",
      bubbles: true,
      cancelable: true,
    });
    target.dispatchEvent(press);
    return press.defaultPrevented;
  };

  it("holds Enter while the year is not one it took, in it or elsewhere", async () => {
    const { wrapper } = render();
    await year(wrapper).setValue("202");

    expect(enter(year(wrapper).element)).toBe(true);
    await flushPromises();
    expect(wrapper.get(".form-field-error").text()).toBe("Год из четырех цифр");
    expect(enter(wrapper.get("#series-number").element)).toBe(true);
  });

  it("lets Enter send a year it took", async () => {
    const { wrapper } = render();
    await year(wrapper).setValue(String(nextYear));
    expect(enter(year(wrapper).element)).toBe(false);
    expect(enter(wrapper.get("#series-number").element)).toBe(false);
  });

  it("sends the year typed in", async () => {
    const { wrapper } = render();
    await year(wrapper).setValue(String(nextYear));
    await create(wrapper);

    expect(createContestSeries).toHaveBeenCalledWith(
      expect.objectContaining({ year: nextYear }),
      FLAGS,
    );
  });
});
