/**
 * @vitest-environment jsdom
 */

/**
 * Editing a catalogue entry of award types, and where the refusal to that edit
 * is written.
 *
 * The window runs the save itself (Dialog `action`), so a refusal is printed in
 * the window over its buttons and the window stays open with everything typed
 * into it. That covers the refusals the response interceptor used to say in a
 * plate over the dialog as well - a 403, a 429, a 5xx - because the request
 * carries the flags that keep the interceptor quiet. Only a request that never
 * arrived stays the interceptor's, and a 401 closes the window.
 *
 * The library tier under the window is reduced to a box that draws the card:
 * the window decides every closing itself, and that is what is read here.
 */
import { afterEach, describe, it, expect, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { setScrollContainer } from "@/shared/lib/scroll";
import AwardTypeEditDialog from "./AwardTypeEditDialog.vue";

const updateAwardType = vi.hoisted(() => vi.fn());

vi.mock("@/entities/achievement", () => ({
  achievementApi: { updateAwardType },
}));

vi.mock("vue-final-modal", () => ({
  VueFinalModal: defineComponent({
    props: { contentClass: { type: String, default: "" } },
    setup:
      (props, { slots }) =>
      () =>
        h(
          "div",
          { class: ["vfm-content", props.contentClass] },
          slots.default?.(),
        ),
  }),
}));

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

  /** A name already taken, and the refusals the interceptor used to say. */
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
