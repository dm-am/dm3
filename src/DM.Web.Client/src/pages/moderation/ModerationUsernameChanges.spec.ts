/**
 * @vitest-environment jsdom
 */

// Why the queue did not arrive is said where it would stand; the statuses the
// interceptor announces are left to it, and neither claims an empty queue.
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

/** The reject dialog has its own spec; the callback the page hands it re-reads the queue. */
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

enableAutoUnmount(afterEach);

const served = (resources: unknown[]) =>
  ({ data: { resources, paging: null }, error: null }) as never;

/** A problem document with the fields the page reads. */
const refused = (status: number, title = "") =>
  ({ data: null, error: { type: "", title, status, traceId: "t" } }) as never;

/** A row as the server sends it: the status by name, no new name before an approval. */
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
        // Passthrough: whether the empty sentence is on screen is what this file reads.
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

    const line = wrapper.get(".error-state");
    expect(line.get(".error-message").text()).toBe("Очередь недоступна");
    expect(line.attributes("role")).toBe("alert");
    expect(wrapper.text()).not.toContain(EMPTY_QUEUE);
    expect(plates()).toEqual([]);
  });

  it("falls back to its own sentence when the server named nothing", async () => {
    read().mockResolvedValue(refused(404));

    const wrapper = render();
    await flushPromises();

    expect(wrapper.get(".error-message").text()).toBe(
      "Не удалось загрузить запросы на смену имени",
    );
  });

  // The line where the queue would be; an empty state there would be a lie.
  it("says a refused load where the queue would be, and claims no empty queue", async () => {
    read().mockResolvedValue(refused(500, "Ошибка сервера"));

    const wrapper = render();
    await flushPromises();

    expect(wrapper.get(".error-message").text()).toBe("Ошибка сервера");
    expect(wrapper.text()).not.toContain(EMPTY_QUEUE);
    expect(plates()).toEqual([]);
  });

  it.each([0, 401])(
    "leaves a load refused with status %i to the interceptor, and claims no empty queue",
    async (status) => {
      read().mockResolvedValue(refused(status, "Ошибка сервера"));

      const wrapper = render();
      await flushPromises();

      expect(wrapper.find(".error-state").exists()).toBe(false);
      expect(wrapper.text()).not.toContain(EMPTY_QUEUE);
      expect(plates()).toEqual([]);
    },
  );

  // The server writes the status by name, not as a number.
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

  it("claims an empty queue only when the server answered one", async () => {
    read().mockResolvedValue(served([]));

    const wrapper = render();
    await flushPromises();

    expect(wrapper.text()).toContain(EMPTY_QUEUE);
    expect(wrapper.find(".error-state").exists()).toBe(false);
  });

  it("takes the refusal off the screen when the queue is read again", async () => {
    read()
      .mockResolvedValueOnce(refused(404))
      .mockResolvedValueOnce(served([request]));

    const wrapper = render();
    await flushPromises();
    expect(wrapper.find(".error-state").exists()).toBe(true);

    // A rejection that went through re-reads the queue.
    await dialog.onSuccess();
    await flushPromises();

    expect(wrapper.find(".error-state").exists()).toBe(false);
    expect(wrapper.find(".request-card").text()).toContain("Иван");
  });

  it("reads the queue again from the line's own retry", async () => {
    read()
      .mockResolvedValueOnce(refused(500, "Ошибка сервера"))
      .mockResolvedValueOnce(served([request]));

    const wrapper = render();
    await flushPromises();
    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(wrapper.find(".error-state").exists()).toBe(false);
    expect(wrapper.find(".request-card").text()).toContain("Иван");
  });

  // Shown cards stay: a read again after a rejection does not blank the queue.
  it("keeps the cards standing under aria-busy while it reads the queue again", async () => {
    read().mockResolvedValueOnce(served([request]));
    const wrapper = render();
    await flushPromises();
    read().mockReturnValueOnce(new Promise(() => {}));

    void dialog.onSuccess();
    await flushPromises();

    expect(wrapper.find(".request-card").exists()).toBe(true);
    expect(wrapper.get(".requests-list").attributes("aria-busy")).toBe("true");
    expect(wrapper.text()).not.toContain("Загрузка...");
  });
});

// The confirmation runs the approval, prints a refusal in itself and closes on success alone.
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

// The menu row, the tab and the heading say one thing.
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
