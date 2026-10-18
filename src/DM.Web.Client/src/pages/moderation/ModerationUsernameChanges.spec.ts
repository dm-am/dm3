/**
 * @vitest-environment jsdom
 */

/**
 * The queue of username change requests, and the one question it answers by
 * itself: why the queue did not arrive.
 *
 * A failed load is the state of the page and not a plate over it: there is no
 * list underneath to go back to, and a plate in the corner over an empty
 * section explains nothing (UI_STANDARDS, "Всплывающие сообщения"). The api
 * method has no seam for `ownsRefusal`, so the page keeps quiet on the
 * statuses the interceptor announces - and makes no claim of an empty queue
 * for them either, because "Нет активных запросов" over a dead server is a
 * lie - and prints the rest where the list would stand, in the server's words.
 *
 * No plate is raised in any test here: the interceptor lives in the axios
 * instance and the api is doubled above it, so what is measured is that the
 * page itself says nothing in the corner.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  mount,
  flushPromises,
  enableAutoUnmount,
  RouterLinkStub,
} from "@vue/test-utils";
import { moderationApi } from "@/entities/moderation";
import { useToast } from "@/shared/lib/composables/useToast";
import { MODERATION_SECTIONS } from "@/shared/config/moderationSections";
import ModerationUsernameChanges from "./ModerationUsernameChanges.vue";

/**
 * The reject dialog has its own spec. What matters here is the callback the
 * page hands it, because a rejection that went through is what re-reads the
 * queue.
 */
const dialog = vi.hoisted(() => ({
  onSuccess: (async () => {}) as () => Promise<void>,
}));

vi.mock("vue-final-modal", () => ({
  useModal: (options: { attrs?: { onSuccess?: () => Promise<void> } }) => {
    if (options.attrs?.onSuccess) dialog.onSuccess = options.attrs.onSuccess;
    return { open: vi.fn(), close: vi.fn() };
  },
}));

vi.mock("./dialogs/UsernameChangeRejectDialog.vue", () => ({
  default: { template: "<div />" },
}));

/** The gate has its own spec; here the viewer is a moderator. */
vi.mock("./lib/useRoleGate", async () => {
  const { ref } = await import("vue");
  return {
    useRoleGate: () => ({ hasAccess: ref(true), deniedText: "" }),
  };
});

enableAutoUnmount(afterEach);

const served = (resources: unknown[]) =>
  ({ data: { resources, paging: null }, error: null }) as never;

/** A problem document with the fields the page reads. */
const refused = (status: number, title = "") =>
  ({ data: null, error: { type: "", title, status, traceId: "t" } }) as never;

/**
 * A row of the queue as the server sends it: the status by its name, as every
 * enum of the API is written, and the empty fields left out. A new name is
 * chosen only after an approval, so a waiting request has none.
 */
const request = {
  id: "r-1",
  userId: "u-1",
  currentUsername: "Иван",
  reason: "Опечатка в имени",
  status: "Pending",
  createdUtc: "2026-09-01T10:00:00Z",
};

const EMPTY_QUEUE = "Нет активных запросов";

function render() {
  return mount(ModerationUsernameChanges, {
    global: {
      stubs: {
        RouterLink: RouterLinkStub,
        HumanDate: true,
        // The confirmation of an approval is the real window, drawn in place.
        Teleport: true,
        // Passthrough: the empty state is a sentence inside it, and whether
        // that sentence is on screen is what this file reads.
        SecondaryText: { template: "<p class='secondary'><slot /></p>" },
      },
    },
  });
}

/** What the corner of the screen says, if anything. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

const read = () => vi.spyOn(moderationApi, "getPendingUsernameChangeRequests");

describe("the username change queue", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    for (const toast of [...toasts.value]) dismiss(toast.id);
  });

  afterEach(() => vi.restoreAllMocks());

  it("prints why the queue did not arrive where the queue would stand, in the server's words", async () => {
    read().mockResolvedValue(refused(404, "Очередь недоступна"));

    const wrapper = render();
    await flushPromises();

    const line = wrapper.get(".form-error");
    expect(line.text()).toBe("Очередь недоступна");
    expect(line.attributes("role")).toBe("alert");
    expect(wrapper.text()).not.toContain(EMPTY_QUEUE);
    expect(plates()).toEqual([]);
  });

  it("falls back to its own sentence when the server named nothing", async () => {
    read().mockResolvedValue(refused(404));

    const wrapper = render();
    await flushPromises();

    expect(wrapper.get(".form-error").text()).toBe(
      "Не удалось загрузить запросы на смену имени",
    );
  });

  /**
   * The statuses the response interceptor used to plate stand in the line
   * where the queue would be, and the plate is taken down (refusalHere). An
   * expired session and a dead connection keep the plate. An empty state in
   * either case would be a lie.
   */
  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
    [503, "Ошибка сервера"],
  ])(
    "says a load refused with status %i where the queue would be, and claims no empty queue",
    async (status, line) => {
      read().mockResolvedValue(refused(status, "Ошибка сервера"));

      const wrapper = render();
      await flushPromises();

      expect(wrapper.get(".form-error").text()).toBe(line);
      expect(wrapper.text()).not.toContain(EMPTY_QUEUE);
      expect(plates()).toEqual([]);
    },
  );

  it.each([0, 401])(
    "leaves a load refused with status %i to the interceptor, and claims no empty queue",
    async (status) => {
      read().mockResolvedValue(refused(status, "Ошибка сервера"));

      const wrapper = render();
      await flushPromises();

      expect(wrapper.find(".form-error").exists()).toBe(false);
      expect(wrapper.text()).not.toContain(EMPTY_QUEUE);
      expect(plates()).toEqual([]);
    },
  );

  /**
   * The server writes the status by name. The page compared it with the
   * number 0, matched nothing, and said "Нет активных запросов" over a queue
   * with requests waiting in it.
   */
  it('shows a request whose status the server wrote as "Pending"', async () => {
    read().mockResolvedValue(served([request]));

    const wrapper = render();
    await flushPromises();

    const cards = wrapper.findAll(".request-card");
    expect(cards).toHaveLength(1);
    expect(cards[0].text()).toContain("Иван");
    expect(cards[0].text()).toContain("Опечатка в имени");
    expect(wrapper.text()).not.toContain(EMPTY_QUEUE);
  });

  /**
   * The queue holds the waiting requests alone (the server selects them), so
   * a word of the state is the same on every row and says nothing. It stood
   * there as "ОЖИДАЕТ" in capitals on a yellow pill.
   */
  it("writes no word of the state on a request", async () => {
    read().mockResolvedValue(served([request]));

    const wrapper = render();
    await flushPromises();

    const card = wrapper.get(".request-card");
    expect(card.text().toLowerCase()).not.toContain("ожидает");
    expect(card.find(".status-badge").exists()).toBe(false);
  });

  it("claims an empty queue only when the server answered one", async () => {
    read().mockResolvedValue(served([]));

    const wrapper = render();
    await flushPromises();

    expect(wrapper.text()).toContain(EMPTY_QUEUE);
    expect(wrapper.find(".form-error").exists()).toBe(false);
  });

  it("takes the refusal off the screen when the queue is read again", async () => {
    read()
      .mockResolvedValueOnce(refused(404))
      .mockResolvedValueOnce(served([request]));

    const wrapper = render();
    await flushPromises();
    expect(wrapper.find(".form-error").exists()).toBe(true);

    // A rejection that went through re-reads the queue.
    await dialog.onSuccess();
    await flushPromises();

    expect(wrapper.find(".form-error").exists()).toBe(false);
    expect(wrapper.find(".request-card").text()).toContain("Иван");
  });
});

/**
 * Approving a request. The confirmation runs the request itself: it waits for
 * the answer, prints a refusal in itself and closes on success alone, and the
 * request carries the flags that keep the interceptor quiet about it.
 */
describe("approving a new name", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    for (const toast of [...toasts.value]) dismiss(toast.id);
  });

  afterEach(() => vi.restoreAllMocks());

  async function approve(answer: unknown) {
    const reads = read().mockResolvedValue(served([request]));
    const resolve = vi
      .spyOn(moderationApi, "resolveUsernameChangeRequest")
      .mockResolvedValue(answer as never);
    const wrapper = render();
    await flushPromises();

    await wrapper.get(".approve-btn").trigger("click");
    await wrapper.get(".dialog-btn-submit").trigger("click");
    await flushPromises();
    return { wrapper, reads, resolve };
  }

  it("approves with the window's flags and reads the queue again", async () => {
    const { wrapper, reads, resolve } = await approve({
      data: null,
      error: null,
    });

    expect(resolve).toHaveBeenCalledWith(
      "r-1",
      { status: "Approved" },
      { ownsRefusal: true, ownsRateLimit: true },
    );
    expect(reads).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
  });

  it("prints a refused approval in the window and stays open", async () => {
    const { wrapper, reads } = await approve(refused(409, "Имя уже занято"));

    expect(wrapper.get(".dialog-refusal").text()).toBe("Имя уже занято");
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(reads).toHaveBeenCalledTimes(1);
    expect(plates()).toEqual([]);
    // The row is live again for the next try.
    expect(wrapper.get(".approve-btn").attributes("disabled")).toBeUndefined();
  });
});

/**
 * The menu row, the tab and the heading say one thing. The section used to sit
 * under the "Модерация" heading of a tab strip and name itself a second time
 * in a subheading, "Запросы на смену имени пользователя": a name that broke a
 * line of the menu on a phone and said nothing the section did not.
 */
describe("the heading of the queue", () => {
  afterEach(() => vi.restoreAllMocks());

  it("is the words of its menu row, and the only heading on the page", async () => {
    read().mockResolvedValue(served([]));

    const wrapper = render();
    await flushPromises();

    const headings = wrapper
      .findAll("page-title, h1, h2, h3")
      .map((heading) => heading.text());
    expect(headings).toEqual([
      MODERATION_SECTIONS.find(
        (section) => section.name === "moderation-username-changes",
      )?.label,
    ]);
  });
});
