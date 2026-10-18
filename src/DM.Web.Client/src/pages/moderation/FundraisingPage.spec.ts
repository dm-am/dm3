/**
 * @vitest-environment jsdom
 */

/**
 * The admin form behind the donation goal, and the one thing it cannot show by
 * itself: that a save worked.
 *
 * The screen after a save is the screen before it - the title, the target and
 * the collected amount, in the fields the administrator typed them into. The
 * answer used to be a plate in the corner, which is gone from here, so the
 * button is now the whole of it: "Сохранено" and inert while the form matches
 * what the server sent back, "Сохранить" again the moment a field changes.
 *
 * Three ways that can go wrong are checked here, and each one is a button that
 * lies. A form nobody has saved must not open reading "Сохранено". A form the
 * server refused must not read it either, and the button has to keep offering
 * the save the administrator still owes. And the claim has to fall away on the
 * first keystroke after it, which is what a snapshot taken by reference instead
 * of by value would quietly break.
 *
 * A refusal is answered in the same place, for the same reason: the form is
 * what the administrator is looking at. It used to be answered by a plate in
 * the corner raised on every status at once, so an expired session, a refusal
 * and a dead server each put two red boxes on screen - the interceptor's and
 * this one's - while the one status the page really owns, a rejected form, got
 * a fallback sentence with the server's own verdict thrown away. The tests
 * below hold both halves: the sentence stands in the form and comes out of the
 * answer, and on the statuses the interceptor speaks for the page is silent.
 */
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

/** The role gate has its own spec; here the viewer is an administrator. */
vi.mock("./lib/useRoleGate", async () => {
  const { ref } = await import("vue");
  return { useRoleGate: () => ({ hasAccess: ref(true), deniedText: "" }) };
});

const goal = (over: Record<string, unknown> = {}) => ({
  title: "На сервер",
  goalAmount: 30000,
  collectedAmount: 12000,
  modifiedUtc: "2026-09-01T10:00:00Z",
  ...over,
});

const served = (resource: unknown) => ({ data: { resource }, error: null });

enableAutoUnmount(afterEach);

/**
 * The real Button, not a stub: what is checked here is a caption and a
 * disabled attribute, and a stub would be answering for both.
 */
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

/** The control the form is saved with. */
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

  /**
   * The title is trimmed on the way to the server, so a space typed on the end
   * of a saved one is not a change: offering a save there would promise the
   * administrator something the server would store identically.
   */
  it("does not call a trailing space a change", async () => {
    const wrapper = await rendered();
    await save(wrapper);

    await wrapper.get("#fundraising-title").setValue("На сервер ");

    expect(saveButton(wrapper).text()).toBe("Сохранено");
  });

  /**
   * The one status this page owns. The server names the field and the rule -
   * a title of nothing but spaces is `title: ["Empty"]` - and that verdict is
   * what the administrator needs, in the form, beside the field it is about.
   */
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
    // Neither the fallback the page used to show on every failure alike, nor
    // the field name the server spells it with.
    expect(refusal.text()).not.toContain("title");
    expect(plates()).toEqual([]);
    expect(refusal.attributes("role")).toBe("alert");

    // The form is still standing, with everything that was typed into it: the
    // refusal must not go into `loadError`, which stands instead of the form.
    expect(wrapper.find(".fundraising-form").exists()).toBe(true);
    expect(
      (wrapper.get("#fundraising-title").element as HTMLInputElement).value,
    ).toBe("   ");
    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();
  });

  /**
   * The statuses the response interceptor used to plate stand in the form's
   * line now, and the plate is taken down (refusalHere; the owner's rule of
   * 26 September 2026). An expired session and a dead connection keep the
   * plate, and the page keeps quiet on them.
   *
   * No plate is raised in this test either, because the interceptor lives in
   * the axios instance and the api is mocked away here - what is measured is
   * that the page itself says nothing, in the form or in the corner.
   */
  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
    [503, "Ошибка сервера"],
  ])("says a failure of status %i in the form", async (status, line) => {
    const wrapper = await rendered();
    updateFundraising.mockResolvedValue({
      data: null,
      error: { status, title: "Ошибка сервера" },
    });

    await wrapper.get("#fundraising-collected").setValue(15000);
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(wrapper.get(".form-error").text()).toBe(line);
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
      // Silent about the failure, but not about the save: it never happened,
      // so the button still owes it.
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

    // A sentence left standing over a changed field reads as a verdict on
    // what is in it now.
    expect(wrapper.find(".form-error").exists()).toBe(false);
  });
});
