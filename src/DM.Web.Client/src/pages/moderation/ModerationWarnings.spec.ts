/**
 * @vitest-environment jsdom
 */

// A page of the log in the server's order: the number in the address reaches
// the request, a correction re-reads the page the moderator is on, and the way
// back from an empty page leads where it says. The name filter goes both ways
// through the address, and the paging link carries it. The card's look is its
// own spec; the page owns the controls it puts in it, offered by the server's
// answer on the row, and no picture removal on the moderator's own record.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  flushPromises,
  enableAutoUnmount,
  type VueWrapper,
} from "@vue/test-utils";
import type { Router } from "vue-router";
import { createPinia, getActivePinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { DEFAULT_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import { mountPage } from "@/shared/lib/testing/mountPage";
import ModerationWarnings from "./ModerationWarnings.vue";

const getAllWarnings = vi.hoisted(() => vi.fn());
const removeWarningProfilePicture = vi.hoisted(() => vi.fn());

vi.mock("@/features/moderation-actions", () => ({
  ChangeWarningPointsDialog: { template: "<div class='dialog-stub' />" },
}));

vi.mock("@/entities/moderation", () => ({
  warningApi: { getAllWarnings, removeWarningProfilePicture },
  WarningCard: {
    props: ["warning"],
    template:
      "<div class='card-stub'><span class='reason-stub'>{{ warning.reason }}</span><div class='record-controls-stub'><slot name='record-controls' /></div><div class='controls-stub'><slot name='controls' /></div></div>",
  },
  RecordSkeleton: {
    props: ["count"],
    template: "<div class='skeleton-stub' />",
  },
}));

/** The correction dialog is opened by hand: the seam is the options handed over. */
const modalOpen = vi.hoisted(() => vi.fn());
const modalClose = vi.hoisted(() => vi.fn());
const modal = vi.hoisted(() => ({
  attrs: null as Record<string, unknown> | null,
}));

// The rest of the module stays real: the page is mounted with its container.
vi.mock("vue-final-modal", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-final-modal")>()),
  useModal: (options: { attrs: Record<string, unknown> }) => {
    modal.attrs = options.attrs;
    return { open: modalOpen, close: modalClose };
  },
}));

const toastSuccess = vi.hoisted(() => vi.fn());

/** The site's window: what it says and runs is this page's, its wait and refusal its own spec. */
vi.mock("@/shared/ui/ConfirmDialog", () => ({
  ConfirmDialog: {
    props: ["show", "title", "message", "confirmLabel", "failure", "action"],
    emits: ["update:show"],
    template:
      "<div v-if='show' class='confirm-stub' :data-title='title' :data-message='message' :data-confirm='confirmLabel' :data-failure='failure'><button class='confirm-go' @click='action()' /><button class='confirm-cancel' @click=\"$emit('update:show', false)\" /></div>",
  },
}));

const WARNINGS = "/moderation/warnings";

let router: Router;
/** Where the log stands: its route and the filter in the address. */
const where = () => ({
  name: router.currentRoute.value.name,
  query: router.currentRoute.value.query,
});

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({ success: toastSuccess, error: vi.fn() }),
}));

/** The page of a reader who chose no size. */
const PAGE_SIZE = DEFAULT_PAGE_SIZE;

const warning = (over: Record<string, unknown> = {}) => ({
  id: "w-1",
  user: { username: "Нарушитель" },
  moderator: { username: "Модератор" },
  points: 2,
  originalPoints: 2,
  reason: "Оскорбление",
  createdUtc: "2026-05-01T12:00:00",
  canChangePoints: true,
  ...over,
});

const page = (resources: unknown[], total = resources.length, pages = 1) => ({
  data: {
    resources,
    paging: { skip: 0, take: PAGE_SIZE, total, current: 1, pages },
  },
  error: null,
});

enableAutoUnmount(afterEach);

// The viewer is a moderator.
beforeEach(() => {
  setActivePinia(createPinia());
  useAuthStore().updateUser({ id: "u-me", username: "Модератор" } as never);
});

/** The paging control is real only where the address it links to is the subject. */
async function render(
  address = WARNINGS,
  { realPaging = false }: { realPaging?: boolean } = {},
) {
  const mounted = await mountPage(ModerationWarnings, address, {
    routes: [
      ["moderation-warnings", WARNINGS],
      ["profile", "/users/:username"],
    ],
    pinia: getActivePinia(),
    stubs: {
      PageTitle: { template: "<h1><slot /></h1>" },
      "page-title": { template: "<h1><slot /></h1>" },
      // Plain, so the assertions read the page's own words.
      FormField: {
        props: ["label", "name"],
        template:
          "<div class='field-stub'><label>{{ label }}</label><slot /></div>",
      },
      Button: { template: "<button><slot /></button>" },
      ...(realPaging
        ? {}
        : {
            Paging: {
              props: ["paging"],
              template: "<nav class='paging-stub' />",
            },
          }),
      ErrorState: { template: "<div class='error-stub' />" },
    },
  });
  router = mounted.router;
  return mounted.wrapper;
}

/** The field of the search line. */
function nameField(wrapper: VueWrapper) {
  return wrapper.find<HTMLInputElement>("form input");
}

async function rendered(address?: string, options?: { realPaging?: boolean }) {
  const wrapper = await render(address, options);
  await flushPromises();
  return wrapper;
}

describe("the moderation warnings page", () => {
  beforeEach(() => {
    getAllWarnings.mockReset();
    modalOpen.mockReset();
    modalClose.mockReset();
    toastSuccess.mockReset();
  });

  it("asks for the page named in the address, at the size of the list", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered(`${WARNINGS}?number=3`);

    expect(getAllWarnings).toHaveBeenCalledWith({
      skip: 2 * PAGE_SIZE,
      take: PAGE_SIZE,
    });
    expect(wrapper.find(".paging-stub").exists()).toBe(true);
  });

  it("keeps the order the server sent, oldest row included", async () => {
    getAllWarnings.mockResolvedValue(
      page([
        warning({
          id: "w-1",
          reason: "Старое",
          createdUtc: "2026-01-01T00:00:00",
        }),
        warning({
          id: "w-2",
          reason: "Новое",
          createdUtc: "2026-05-01T00:00:00",
        }),
      ]),
    );

    const wrapper = await rendered();

    expect(wrapper.findAll(".reason-stub").map((row) => row.text())).toEqual([
      "Старое",
      "Новое",
    ]);
  });

  it("puts the control that changes the points inside the card", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();

    expect(wrapper.find(".controls-stub .caption-action").text()).toBe(
      "Изменить баллы",
    );
  });

  // The server has decided for this row whether the reader may correct it.
  it("offers no control on a row the server did not open", async () => {
    getAllWarnings.mockResolvedValue(
      page([warning({ canChangePoints: false })]),
    );

    const wrapper = await rendered();

    expect(wrapper.find(".caption-action").exists()).toBe(false);
  });

  it("opens the form on the warning the control belongs to", async () => {
    getAllWarnings.mockResolvedValue(page([warning({ id: "w-7", points: 4 })]));

    const wrapper = await rendered();
    await wrapper.find(".caption-action").trigger("click");

    expect(modalOpen).toHaveBeenCalled();
    expect(modal.attrs?.warningId).toBe("w-7");
    expect(modal.attrs?.currentPoints).toBe(4);
  });

  // A warning is never withdrawn, so the page re-read is the one the moderator is on.
  it("re-reads the page the moderator is on after a correction", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered(`${WARNINGS}?number=2`);
    await wrapper.find(".caption-action").trigger("click");
    await (modal.attrs?.onSuccess as () => Promise<void>)();
    await flushPromises();

    expect(modalClose).toHaveBeenCalled();
    expect(toastSuccess).not.toHaveBeenCalled();
    expect(getAllWarnings).toHaveBeenCalledTimes(2);
    expect(getAllWarnings).toHaveBeenLastCalledWith({
      skip: PAGE_SIZE,
      take: PAGE_SIZE,
    });
  });

  /** A form closed without saving changes nothing and says nothing. */
  it("re-reads nothing when the form is closed unsaved", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();
    await wrapper.find(".caption-action").trigger("click");
    (modal.attrs?.onCancel as () => void)();
    await flushPromises();

    expect(modalClose).toHaveBeenCalled();
    expect(toastSuccess).not.toHaveBeenCalled();
    expect(getAllWarnings).toHaveBeenCalledTimes(1);
  });

  // The page says so and offers the way back instead of moving the reader.
  it("offers the first page when this one is past the end of a filled log", async () => {
    getAllWarnings.mockResolvedValue(page([], 3));

    const wrapper = await rendered(`${WARNINGS}?number=4`);

    expect(wrapper.text()).toContain("На этой странице предупреждений нет");
    const back = wrapper.find(".warnings-empty a");
    expect(back.text()).toBe("Вернуться на первую страницу");
    expect(back.attributes("href")).toBe(WARNINGS);
  });

  // A second click of the paging control is enough to put two pages on the wire.
  it("keeps the page the address names when a later answer belongs to an older one", async () => {
    const answers = new Map<number, (value: unknown) => void>();
    getAllWarnings.mockImplementation(
      ({ skip }: { skip: number }) =>
        new Promise((resolve) => {
          answers.set(skip, resolve);
        }),
    );

    const wrapper = await render();
    await flushPromises();

    await router.push(`${WARNINGS}?number=2`);
    await flushPromises();

    answers.get(PAGE_SIZE)?.(page([warning({ id: "w-2", reason: "Новое" })]));
    await flushPromises();
    answers.get(0)?.(page([warning({ reason: "Старое" })]));
    await flushPromises();

    expect(wrapper.findAll(".reason-stub").map((row) => row.text())).toEqual([
      "Новое",
    ]);
  });

  it("says the log is empty only when it really is", async () => {
    getAllWarnings.mockResolvedValue(page([]));

    const wrapper = await rendered();

    expect(wrapper.text()).toContain("Предупреждений пока нет");
  });

  // The search line of the uploads list: the server finds the exact name.
  it("draws the search line the neighbouring moderation lists draw", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();

    expect(wrapper.find("label").exists()).toBe(false);
    expect(wrapper.find("form .search-container").exists()).toBe(true);
    expect(nameField(wrapper).attributes("placeholder")).toBe(
      "Точное имя пользователя",
    );
    expect(wrapper.find("form button[type=submit]").text()).toBe("Найти");
  });

  // An emptied field drops the filter without "Найти".
  it("drops the name at once when the field is emptied", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered(`${WARNINGS}?username=Нарушитель&number=2`);
    await wrapper.find("form .clear-input-btn").trigger("click");
    await flushPromises();

    expect(where()).toEqual({ name: "moderation-warnings", query: {} });
    expect(getAllWarnings).toHaveBeenLastCalledWith({
      username: undefined,
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  /** Typing a name is no search yet: the server looks up whole names. */
  it("asks for nothing while the name is being typed", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();
    await nameField(wrapper).setValue("Нару");
    await flushPromises();

    expect(where()).toEqual({ name: "moderation-warnings", query: {} });
    expect(getAllWarnings).toHaveBeenCalledTimes(1);
  });

  it("asks for the name the address names", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    await rendered(`${WARNINGS}?username=Нарушитель`);

    expect(getAllWarnings).toHaveBeenCalledWith({
      username: "Нарушитель",
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  /** A link handed to another moderator opens with the filter already applied. */
  it("shows the name the address names in the field", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered(`${WARNINGS}?username=Нарушитель`);

    expect(nameField(wrapper).element.value).toBe("Нарушитель");
  });

  // The page number counted pages of the list being left, so it stays behind.
  it("puts the name in the address without the page number of the previous list", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered(`${WARNINGS}?number=3`);
    await nameField(wrapper).setValue("  Нарушитель  ");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(where()).toEqual({
      name: "moderation-warnings",
      query: { username: "Нарушитель" },
    });
    expect(getAllWarnings).toHaveBeenLastCalledWith({
      username: "Нарушитель",
      skip: 0,
      take: PAGE_SIZE,
    });
    // The field reads the address back, padding gone.
    expect(nameField(wrapper).element.value).toBe("Нарушитель");
  });

  it("takes the name out of the address when the field is emptied", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered(`${WARNINGS}?username=Нарушитель&number=2`);
    await nameField(wrapper).setValue("");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(where()).toEqual({ name: "moderation-warnings", query: {} });
    expect(getAllWarnings).toHaveBeenLastCalledWith({
      username: undefined,
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  // The address is already there, so the router hands nothing back; the field still shows the trimmed name.
  it("shows the trimmed name when the address is already the one asked for", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered(`${WARNINGS}?username=Нарушитель`);
    await nameField(wrapper).setValue("  Нарушитель  ");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(nameField(wrapper).element.value).toBe("Нарушитель");
  });

  // The filter and the page number share one address, and the next-page link carries both.
  it("keeps the name in the address the paging control links to", async () => {
    getAllWarnings.mockResolvedValue(page([warning()], 25, 2));

    const wrapper = await rendered(`${WARNINGS}?username=Нарушитель`, {
      realPaging: true,
    });

    const second = wrapper
      .findAll(".paging a")
      .find((link) => link.text() === "2");
    expect(decodeURI(second?.attributes("href") ?? "")).toBe(
      `${WARNINGS}?username=Нарушитель&number=2`,
    );
  });

  // An empty list under a filter is not an empty log.
  it("says nothing was found when the filter matches nobody", async () => {
    getAllWarnings.mockResolvedValue(page([]));

    const wrapper = await rendered(`${WARNINGS}?username=Нарушитель`);

    expect(wrapper.text()).toContain(
      "Предупреждений по заданным фильтрам не найдено",
    );
    expect(wrapper.text()).not.toContain("Предупреждений пока нет");
  });
});

/** A record of a profile, with the avatar of the moment or without one. */
const onProfile = (over: Record<string, unknown> = {}) =>
  warning({
    id: "w-p",
    entityType: "Profile",
    profile: {
      status: "Максимальная длина",
      info: "О себе",
      picture: { originalUrl: "/u/original.png" },
    },
    canChangePoints: false,
    ...over,
  });

describe("the picture of a record of a profile", () => {
  beforeEach(() => {
    getAllWarnings.mockReset();
    removeWarningProfilePicture.mockReset();
  });

  const actionOf = (wrapper: VueWrapper, index: number) =>
    wrapper
      .findAll(".card-stub")
      [index].find(".record-controls-stub .caption-action");

  it("offers to take the picture out only where the record has one", async () => {
    getAllWarnings.mockResolvedValue(
      page([
        onProfile(),
        onProfile({
          id: "w-2",
          profile: { status: "Без картинки", info: null, picture: null },
        }),
        onProfile({ id: "w-3", profile: null }),
        warning({ id: "w-4", entityType: "Comment" }),
      ]),
    );

    const wrapper = await rendered();

    expect(actionOf(wrapper, 0).text()).toBe("Удалить картинку");
    for (const index of [1, 2, 3])
      expect(actionOf(wrapper, index).exists()).toBe(false);
  });

  // The server would read his own mark as the owner's and keep the picture in the log.
  it("offers nothing on the moderator's own record", async () => {
    getAllWarnings.mockResolvedValue(
      page([onProfile({ user: { id: "u-me", username: "Модератор" } })]),
    );

    const wrapper = await rendered();

    expect(actionOf(wrapper, 0).exists()).toBe(false);
  });

  it("asks before the picture goes, in the site's window", async () => {
    getAllWarnings.mockResolvedValue(page([onProfile()]));

    const wrapper = await rendered();
    expect(wrapper.find(".confirm-stub").exists()).toBe(false);
    await actionOf(wrapper, 0).trigger("click");

    const confirm = wrapper.get(".confirm-stub");
    expect(confirm.attributes("data-title")).toBe("Удалить картинку?");
    expect(confirm.attributes("data-message")).toBe(
      "Картинка пропадет из обоих логов, вернуть ее будет нельзя",
    );
    expect(confirm.attributes("data-confirm")).toBe("Удалить");
    expect(confirm.attributes("data-failure")).toBe(
      "Не удалось удалить картинку",
    );
    expect(removeWarningProfilePicture).not.toHaveBeenCalled();
  });

  // The window prints a refusal in itself; the page is read again after the server agrees.
  it("takes the picture out of the record and re-reads the page", async () => {
    getAllWarnings.mockResolvedValue(page([onProfile()]));
    removeWarningProfilePicture.mockResolvedValue({ data: null, error: null });

    const wrapper = await rendered(`${WARNINGS}?number=2`);
    await actionOf(wrapper, 0).trigger("click");
    await wrapper.get(".confirm-go").trigger("click");
    await flushPromises();

    expect(removeWarningProfilePicture).toHaveBeenCalledWith("w-p", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(getAllWarnings).toHaveBeenCalledTimes(2);
    expect(getAllWarnings).toHaveBeenLastCalledWith({
      skip: PAGE_SIZE,
      take: PAGE_SIZE,
    });
  });

  it("re-reads nothing when the server refuses", async () => {
    getAllWarnings.mockResolvedValue(page([onProfile()]));
    removeWarningProfilePicture.mockResolvedValue({
      data: null,
      error: { status: 403, message: "Нет" },
    });

    const wrapper = await rendered();
    await actionOf(wrapper, 0).trigger("click");
    await wrapper.get(".confirm-go").trigger("click");
    await flushPromises();

    expect(getAllWarnings).toHaveBeenCalledTimes(1);
  });

  it("sends nothing when the question is closed", async () => {
    getAllWarnings.mockResolvedValue(page([onProfile()]));

    const wrapper = await rendered();
    await actionOf(wrapper, 0).trigger("click");
    await wrapper.get(".confirm-cancel").trigger("click");

    expect(wrapper.find(".confirm-stub").exists()).toBe(false);
    expect(removeWarningProfilePicture).not.toHaveBeenCalled();
  });
});
