/**
 * @vitest-environment jsdom
 */

/**
 * A setting of the account page is saved the moment its box is pressed, and
 * while that save is in flight the boxes used to go disabled. A disabled box
 * that is ticked is a contradiction on screen: the site's disabled state says
 * the control is not on offer, and the tick says it is on. So the boxes stay
 * drawn as they are for the length of the save.
 *
 * What the disabled attribute did besides the drawing still has to hold: a
 * press made during the save does not toggle anything and does not become a
 * second request racing the first. Both sections that save on press are
 * checked, the black list and the notification channels.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  RouterLinkStub,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi, blacklistApi } from "@/entities/user";
import AccountBlacklistSection from "./AccountBlacklistSection.vue";
import AccountNotificationsSection from "./AccountNotificationsSection.vue";

vi.mock("vue-final-modal", async (importOriginal) => {
  const actual = await importOriginal<typeof import("vue-final-modal")>();
  return {
    ...actual,
    useModal: () => ({
      open: vi.fn(),
      close: vi.fn(),
      patchOptions: vi.fn(),
      destroy: vi.fn(),
    }),
  };
});

enableAutoUnmount(afterEach);

/** A save the test finishes by hand, so the section can be read mid-flight. */
const heldSave = () => {
  let finish: () => void = () => {};
  const answer = new Promise((resolve) => {
    finish = () => resolve({ data: null, error: null });
  });
  return { answer, finish };
};

// A box fires its change only while it stands in a document, as a press on
// a page does; the sections are mounted into one for that reason.

/** The box a label names, and its label. */
const boxOf = (wrapper: VueWrapper, label: string) => {
  const row = wrapper.findAll("label").find((l) => l.text() === label);
  if (!row) throw new Error(`no box "${label}"`);
  return row.get('input[type="checkbox"]');
};

const isTicked = (wrapper: VueWrapper, label: string) =>
  (boxOf(wrapper, label).element as HTMLInputElement).checked;

beforeEach(() => {
  setActivePinia(createPinia());
  vi.restoreAllMocks();
});

describe("the black list settings while a save is in flight", () => {
  async function mountSection() {
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue({
      data: {
        hideComments: true,
        hideMessages: false,
        hideGames: false,
        hideBlogs: false,
        blockDirectMessages: false,
        autoPopulateContentBlacklist: false,
      },
      error: null,
    } as never);
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue({
      data: { resources: [] },
      error: null,
    } as never);
    const save = heldSave();
    const update = vi
      .spyOn(blacklistApi, "updateSettings")
      .mockReturnValue(save.answer as never);

    const wrapper = mount(AccountBlacklistSection, {
      global: { stubs: { RouterLink: RouterLinkStub } },
      attachTo: document.body,
    });
    await flushPromises();
    return { wrapper, update, save };
  }

  it("keeps every box, the ticked one too, drawn as it was", async () => {
    const { wrapper } = await mountSection();

    await boxOf(wrapper, "Скрывать их игры").trigger("click");

    for (const box of wrapper.findAll('input[type="checkbox"]')) {
      expect(box.attributes("disabled")).toBeUndefined();
    }
    expect(isTicked(wrapper, "Скрывать их комментарии")).toBe(true);
    expect(isTicked(wrapper, "Скрывать их игры")).toBe(true);
  });

  it("refuses a press during the save, with no second request", async () => {
    const { wrapper, update, save } = await mountSection();

    await boxOf(wrapper, "Скрывать их игры").trigger("click");
    await boxOf(wrapper, "Скрывать их блоги").trigger("click");
    await boxOf(wrapper, "Скрывать их комментарии").trigger("click");

    expect(update).toHaveBeenCalledTimes(1);
    expect(isTicked(wrapper, "Скрывать их блоги")).toBe(false);
    expect(isTicked(wrapper, "Скрывать их комментарии")).toBe(true);

    // Once the answer is in, a press is a press again.
    save.finish();
    await flushPromises();
    await boxOf(wrapper, "Скрывать их блоги").trigger("click");
    expect(update).toHaveBeenCalledTimes(2);
    expect(isTicked(wrapper, "Скрывать их блоги")).toBe(true);
  });
});

describe("the notification channels while a save is in flight", () => {
  async function mountSection() {
    vi.spyOn(accountApi, "getNotificationPreferences").mockResolvedValue({
      data: {
        telegram: {
          connected: true,
          enabled: true,
          enabledCategories: ["Forum"],
        },
        discord: undefined,
      },
      error: null,
    } as never);
    const save = heldSave();
    const update = vi
      .spyOn(accountApi, "updateNotificationPreferences")
      .mockReturnValue(save.answer as never);

    const wrapper = mount(AccountNotificationsSection, {
      global: { plugins: [createPinia()] },
      attachTo: document.body,
    });
    await flushPromises();
    return { wrapper, update, save };
  }

  it("keeps every box, the ticked ones too, drawn as they were", async () => {
    const { wrapper } = await mountSection();

    await boxOf(wrapper, "Игры").trigger("click");

    for (const box of wrapper.findAll('input[type="checkbox"]')) {
      expect(box.attributes("disabled")).toBeUndefined();
    }
    expect(isTicked(wrapper, "Включить уведомления")).toBe(true);
    expect(isTicked(wrapper, "Форум")).toBe(true);
  });

  it("refuses a press during the save, with no second request", async () => {
    const { wrapper, update, save } = await mountSection();

    await boxOf(wrapper, "Игры").trigger("click");
    await boxOf(wrapper, "Блоги").trigger("click");
    await boxOf(wrapper, "Включить уведомления").trigger("click");

    expect(update).toHaveBeenCalledTimes(1);
    expect(isTicked(wrapper, "Блоги")).toBe(false);
    expect(isTicked(wrapper, "Включить уведомления")).toBe(true);

    save.finish();
    await flushPromises();
    await boxOf(wrapper, "Блоги").trigger("click");
    expect(update).toHaveBeenCalledTimes(2);
  });
});
