/**
 * @vitest-environment jsdom
 */

// The window runs the save: a refusal is printed in it and it stays open with
// everything typed; the request carries the flags that keep the interceptor quiet.
import { afterEach, describe, it, expect, vi, beforeEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { setScrollContainer } from "@/shared/lib/scroll";
import AwardTypeEditDialog from "./AwardTypeEditDialog.vue";

const updateAwardType = vi.hoisted(() => vi.fn());

vi.mock("@/entities/achievement", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/achievement")>()),
  achievementApi: { updateAwardType },
}));

vi.mock("vue-final-modal", async () =>
  (await import("@/shared/lib/testing/vueFinalModalStub")).vueFinalModal(),
);

const AWARD_TYPE = {
  id: "at-1",
  code: "PEOPLES_CHOICE",
  title: "Народное признание, например",
  description: "выбор читателей",
  iconName: "trophy",
  tier: 1,
  sortOrder: 3,
  isActive: true,
};

const FLAGS = { ownsRefusal: true, ownsRateLimit: true };

enableAutoUnmount(afterEach);

afterEach(() => {
  document.body.innerHTML = "";
  setScrollContainer(null);
});

/** The window as useModal opens it, with the library's closing recorded. */
function render() {
  const shut = vi.fn();
  const wrapper = mount(AwardTypeEditDialog, {
    attachTo: document.body,
    props: {
      awardType: AWARD_TYPE,
      modelValue: true,
      "onUpdate:modelValue": shut,
    } as never,
  });
  return { wrapper, shut };
}

async function save(wrapper: ReturnType<typeof render>["wrapper"]) {
  await wrapper.get("form").trigger("submit");
  await flushPromises();
}

const refused = (status: number, title = "Ошибка сервера") => ({
  data: null,
  error: { type: "Failure", status, title, traceId: "t" },
});

describe("the award type dialog", () => {
  beforeEach(() => {
    updateAwardType.mockReset();
    updateAwardType.mockResolvedValue({ data: null, error: null });
  });

  it("saves the entry with the window's flags and closes", async () => {
    const { wrapper, shut } = render();
    await save(wrapper);

    expect(updateAwardType).toHaveBeenCalledWith(
      "at-1",
      {
        title: AWARD_TYPE.title,
        description: AWARD_TYPE.description,
        iconName: AWARD_TYPE.iconName,
        tier: 1,
        sortOrder: 3,
      },
      FLAGS,
    );
    expect(wrapper.emitted("success")).toBeTruthy();
    expect(shut).toHaveBeenCalledWith(false);
    expect(wrapper.find(".form-refusal").exists()).toBe(false);
  });

  /** A name already taken, and the refusals the window takes over from the interceptor. */
  it.each([
    [409, "Такое название уже занято"],
    [403, "Недостаточно прав"],
    [500, "Ошибка сервера"],
  ])(
    "prints a refusal of status %i in the window and stays open",
    async (status, title) => {
      updateAwardType.mockResolvedValue(refused(status, title));
      const { wrapper, shut } = render();
      await save(wrapper);

      expect(wrapper.get(".form-refusal").text()).toBe(title);
      expect(wrapper.emitted("success")).toBeFalsy();
      expect(shut).not.toHaveBeenCalled();
      // Everything typed is still there, and the button is live again.
      expect(
        (wrapper.get("#award-type-title").element as HTMLInputElement).value,
      ).toBe(AWARD_TYPE.title);
      expect(
        wrapper.get(".controls button.primary").attributes("disabled"),
      ).toBeUndefined();
    },
  );

  it("says its own sentence when the server named nothing", async () => {
    updateAwardType.mockResolvedValue(refused(404, ""));
    const { wrapper } = render();
    await save(wrapper);

    expect(wrapper.get(".form-refusal").text()).toBe("Не удалось сохранить");
  });

  it("leaves a request that never arrived to the interceptor", async () => {
    updateAwardType.mockResolvedValue(refused(0));
    const { wrapper, shut } = render();
    await save(wrapper);

    expect(wrapper.find(".form-refusal").exists()).toBe(false);
    expect(shut).not.toHaveBeenCalled();
  });
});

/** The fields of the window, by their label: the words on screen and the bounds. */
describe("the fields of the award type dialog", () => {
  const field = (
    wrapper: ReturnType<typeof render>["wrapper"],
    label: string,
  ) =>
    wrapper
      .findAll(".form-field")
      .find((f) => f.find(".form-field-label label").text() === label)!;

  it("names its fields in Russian, with the icon set and the scale under them", () => {
    const { wrapper } = render();
    const labels = wrapper
      .findAll(".form-field-label label")
      .map((l) => l.text());
    expect(labels).toEqual([
      "Название",
      "Описание",
      "Имя иконки",
      "Тир",
      "Порядок сортировки",
    ]);
    expect(field(wrapper, "Имя иконки").get(".form-field-hint").text()).toBe(
      "Из набора иконок",
    );
    expect(field(wrapper, "Тир").get(".form-field-hint").text()).toBe(
      "1 золото, 2 серебро, 3 бронза, 5 платина",
    );
  });

  it("takes the tier on the scale of the server and the lengths of its columns", () => {
    const { wrapper } = render();
    const tier = wrapper.get("#award-type-tier");
    expect([tier.attributes("min"), tier.attributes("max")]).toEqual([
      "1",
      "5",
    ]);
    expect(wrapper.get("#award-type-title").attributes("maxlength")).toBe(
      "200",
    );
    expect(wrapper.get("#award-type-description").attributes("maxlength")).toBe(
      "1000",
    );
    expect(wrapper.get("#award-type-icon").attributes("maxlength")).toBe("80");
  });
});
