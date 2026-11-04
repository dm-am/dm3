/**
 * @vitest-environment jsdom
 */

/**
 * ChatView, the search and the calendar: the row under the line of the
 * correspondence, a hit landed on, a day picked and the way from it back to
 * the latest messages.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { ChatSearchRow } from "@/widgets/chat-feed";
import { fromSiteInput } from "@/shared/lib/utils/datetime";
import { api, nav, search } from "./ChatView.doubles";
import {
  closeChat,
  heldAnswer,
  messagePage,
  openChat,
  resetChatView,
  shownIds,
} from "./ChatView.fixtures";

vi.mock("@/entities/message/api/messagingApi", async () =>
  (await import("./ChatView.doubles")).messagingApi(),
);
vi.mock("@/shared/lib/composables/useSignalR", async () =>
  (await import("./ChatView.doubles")).signalR(),
);
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeComposer(
    (await import("./ChatView.doubles")).clearSpy,
  ),
);
vi.mock("vue-router", async () =>
  (await import("./ChatView.doubles")).vueRouter(),
);
vi.mock("@/features/message-search/api/messageSearchApi", async () =>
  (await import("./ChatView.doubles")).messageSearchApi(),
);

beforeEach(resetChatView);
afterEach(closeChat);

// The global chat's search and "Перейти к дате" under the line, on the same history window.
describe("ChatView, the search and the calendar", () => {
  /** A message written at noon of a day, so no zone moves it to another. */
  const dated = (id: string, day: string) => ({
    id,
    text: id,
    createdUtc: `${day}T12:00:00Z`,
    likes: [],
  });

  const datedPage = (
    messages: ReturnType<typeof dated>[],
    paging: Record<string, unknown> = {},
  ) => ({
    data: {
      resources: messages,
      paging: {
        prevCursor: null,
        nextCursor: null,
        hasPrev: false,
        hasNext: false,
        ...paging,
      },
    },
    error: null,
  });

  /** The observers of the sentinels, so a test can bring one into view. */
  let observers: {
    callback: IntersectionObserverCallback;
    targets: Element[];
  }[] = [];

  beforeEach(() => {
    observers = [];
    vi.stubGlobal(
      "IntersectionObserver",
      class {
        targets: Element[] = [];
        constructor(callback: IntersectionObserverCallback) {
          observers.push({ callback, targets: this.targets });
        }
        observe(target: Element) {
          this.targets.push(target);
        }
        unobserve() {}
        disconnect() {}
      },
    );
    search.searchMessages.mockReset();
  });

  const row = (wrapper: ReturnType<typeof mount>) =>
    wrapper.findComponent(ChatSearchRow);

  /** "поиск" of the line. */
  const searchItem = (wrapper: ReturnType<typeof mount>) =>
    wrapper
      .findAll(".chat-strip .strip-item")
      .find((item) => item.text() === "поиск");

  async function openRow(wrapper: ReturnType<typeof mount>) {
    await searchItem(wrapper)!.trigger("click");
    await flushPromises();
  }

  const notice = (wrapper: ReturnType<typeof mount>) =>
    wrapper.find(".chat-feed__notice");

  it('opens the row of the global chat under the line on "поиск", the caret in it', async () => {
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat({ attached: true });
    expect(row(wrapper).exists()).toBe(false);
    expect(searchItem(wrapper)!.attributes("aria-expanded")).toBe("false");

    await openRow(wrapper);

    expect(row(wrapper).props("scope")).toBe("dm:c1");
    const input = wrapper.get(".chat-search-row input");
    expect(input.attributes("placeholder")).toBe("Поиск по переписке");
    expect(document.activeElement).toBe(input.element);
    expect(searchItem(wrapper)!.attributes("aria-expanded")).toBe("true");
    // In the frame, under the line.
    expect(
      wrapper
        .find(".chat-feed__frame #conversation-search .chat-search-row")
        .exists(),
    ).toBe(true);

    await searchItem(wrapper)!.trigger("click");
    expect(row(wrapper).exists()).toBe(false);
  });

  it("opens the row on Ctrl+F", async () => {
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat({ attached: true });

    const event = new KeyboardEvent("keydown", {
      key: "f",
      ctrlKey: true,
      cancelable: true,
    });
    document.dispatchEvent(event);
    await flushPromises();

    expect(event.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(
      wrapper.get(".chat-search-row input").element,
    );
  });

  it("offers no search in a correspondence with nothing in it", async () => {
    const wrapper = await openChat();

    expect(wrapper.text()).toContain("Сообщений пока нет");
    expect(searchItem(wrapper)).toBeUndefined();
    const event = new KeyboardEvent("keydown", {
      key: "f",
      ctrlKey: true,
      cancelable: true,
    });
    document.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(false);
  });

  it("offers the search while the first window is on its way", async () => {
    const window = heldAnswer<unknown>();
    api.getMessages.mockReturnValue(window.answer);
    const wrapper = await openChat();

    expect(searchItem(wrapper)).toBeDefined();
    window.settle(messagePage(["m1"]));
  });

  it("lands the feed on a hit of the search", async () => {
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    search.searchMessages.mockResolvedValue({
      data: {
        resources: [
          {
            sourceType: "chat",
            sourceId: "c1",
            sourceTitle: null,
            id: "m5",
            createdUtc: "2026-07-01T10:00:00Z",
            snippet: [{ text: "пятое", isMatch: true }],
          },
        ],
        paging: {
          nextCursor: null,
          prevCursor: null,
          hasNext: false,
          hasPrev: false,
        },
      },
      error: null,
    });
    const wrapper = await openChat();
    await openRow(wrapper);

    const input = wrapper.find(".chat-search-row input");
    await input.setValue("пятое");
    await input.trigger("keydown", { key: "Enter" });
    await flushPromises();
    expect(search.searchMessages.mock.calls[0][0]).toMatchObject({
      in: ["dm:c1"],
    });

    api.getMessages.mockResolvedValue(messagePage(["m4", "m5", "m6"]));
    await wrapper.get(".result-row").trigger("click");
    await flushPromises();

    expect(api.getMessages).toHaveBeenLastCalledWith(
      "c1",
      expect.objectContaining({ aroundMessageId: "m5" }),
    );
    expect(shownIds()).toEqual(["m4", "m5", "m6"]);
  });

  it("puts a picked day in the address and opens the feed on it", async () => {
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();
    await openRow(wrapper);

    api.getMessages.mockResolvedValue(
      datedPage([dated("a", "2026-09-09"), dated("b", "2026-09-10")], {
        hasNext: true,
        nextCursor: "after-b",
      }),
    );
    row(wrapper).vm.$emit("date-picked", "2026-09-10");
    await flushPromises();

    expect(nav.push).toHaveBeenCalledWith({
      name: "chat",
      params: { id: "c1" },
      query: { date: "2026-09-10" },
    });
    expect(api.getMessages).toHaveBeenLastCalledWith(
      "c1",
      expect.objectContaining({
        nearTimestampUtc: fromSiteInput("2026-09-10T00:00"),
      }),
    );
    expect(shownIds()).toEqual(["a", "b"]);
    expect(row(wrapper).props("selectedDate")).toBe("2026-09-10");
    expect(notice(wrapper).exists()).toBe(false);
  });

  it("opens on the day the address names", async () => {
    nav.route.query = { date: "2026-09-10" };
    api.getMessages.mockResolvedValue(
      datedPage([dated("b", "2026-09-10")], { hasPrev: true }),
    );
    const wrapper = await openChat();
    await openRow(wrapper);

    expect(api.getMessages).toHaveBeenCalledTimes(1);
    expect(api.getMessages.mock.calls[0][1]).toHaveProperty("nearTimestampUtc");
    expect(row(wrapper).props("selectedDate")).toBe("2026-09-10");
  });

  it("stands near a day with no message of its own, the day kept, as the global chat does", async () => {
    nav.route.query = { date: "2026-09-10" };
    api.getMessages.mockResolvedValueOnce(
      datedPage([dated("before", "2026-09-05"), dated("later", "2026-09-20")]),
    );
    const wrapper = await openChat();
    await openRow(wrapper);

    expect(notice(wrapper).exists()).toBe(false);
    expect(shownIds()).toEqual(["before", "later"]);
    expect(nav.replace).not.toHaveBeenCalled();
    expect(row(wrapper).props("selectedDate")).toBe("2026-09-10");
    expect(api.getMessages).toHaveBeenCalledTimes(1);
  });

  it("says a linked message is gone over the latest messages", async () => {
    nav.route.query = { msg: "gone" };
    api.getMessages
      .mockResolvedValueOnce(messagePage([]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    const wrapper = await openChat();

    expect(notice(wrapper).text()).toBe(
      "Сообщение по ссылке не найдено или удалено, показаны последние сообщения",
    );
    expect(wrapper.text()).not.toContain("Сообщений пока нет");
    expect(shownIds()).toEqual(["m1", "m2"]);
  });

  it("offers the days from the first message, known from a window that reaches it", async () => {
    api.getMessages.mockResolvedValue(
      datedPage([dated("first", "2026-08-01"), dated("b", "2026-09-10")]),
    );
    const wrapper = await openChat();
    await openRow(wrapper);

    expect(row(wrapper).props("minDate")).toBe("2026-08-01");
    row(wrapper).vm.$emit("date-open");
    await flushPromises();
    expect(api.getMessages).toHaveBeenCalledTimes(1);
  });

  it("asks for the first message when the calendar opens over a longer history", async () => {
    api.getMessages.mockResolvedValue(
      datedPage([dated("b", "2026-09-10")], { hasPrev: true }),
    );
    const wrapper = await openChat();
    await openRow(wrapper);
    expect(row(wrapper).props("minDate")).toBe("");

    api.getMessages.mockResolvedValue(
      datedPage([dated("first", "2025-01-15")]),
    );
    row(wrapper).vm.$emit("date-open");
    await flushPromises();

    expect(api.getMessages).toHaveBeenLastCalledWith("c1", {
      nearTimestampUtc: new Date(0).toISOString(),
      limit: 1,
    });
    expect(row(wrapper).props("minDate")).toBe("2025-01-15");
  });

  // The first day of the correspondence opened before is not this one's.
  it("does not offer the first day of the correspondence opened before", async () => {
    api.getMessages.mockResolvedValue(
      datedPage([dated("b", "2026-09-10")], { hasPrev: true }),
    );
    const wrapper = await openChat();
    await openRow(wrapper);
    api.getMessages.mockResolvedValue(
      datedPage([dated("first", "2026-08-01")]),
    );
    row(wrapper).vm.$emit("date-open");
    await flushPromises();
    expect(row(wrapper).props("minDate")).toBe("2026-08-01");

    api.getChat.mockResolvedValue({
      data: { id: "c2", participants: [], viewer: { canWrite: true } },
      error: null,
    });
    api.getMessages.mockResolvedValue(
      datedPage([dated("y", "2026-09-12")], { hasPrev: true }),
    );
    nav.route.params = { id: "c2" };
    await flushPromises();
    await openRow(wrapper);
    expect(row(wrapper).props("minDate")).toBe("");

    api.getMessages.mockResolvedValue(
      datedPage([dated("first-of-c2", "2025-03-01")]),
    );
    row(wrapper).vm.$emit("date-open");
    await flushPromises();

    expect(api.getMessages).toHaveBeenLastCalledWith("c2", {
      nearTimestampUtc: new Date(0).toISOString(),
      limit: 1,
    });
    expect(row(wrapper).props("minDate")).toBe("2025-03-01");
  });

  // A picked day and the way back carry the list's page (listPage.ts).
  it("keeps the page of the list when a day is picked and left", async () => {
    nav.route.query = { number: "2" };
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();
    await openRow(wrapper);

    api.getMessages.mockResolvedValue(
      datedPage([dated("a", "2026-09-10")], {
        hasNext: true,
        nextCursor: "after-a",
      }),
    );
    row(wrapper).vm.$emit("date-picked", "2026-09-10");
    await flushPromises();

    expect(nav.push).toHaveBeenLastCalledWith({
      name: "chat",
      params: { id: "c1" },
      query: { number: "2", date: "2026-09-10" },
    });

    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    wrapper.findComponent({ name: "ChatFeed" }).vm.$emit("jump-to-latest");
    await flushPromises();

    expect(nav.replace).toHaveBeenLastCalledWith({
      name: "chat",
      params: { id: "c1" },
      query: { number: "2" },
    });
  });

  // From a picked day the feed pages down toward the latest messages, the sentinel of the new window watched.
  it("pages from a picked day toward the latest messages", async () => {
    vi.useFakeTimers({ toFake: ["setTimeout"] });
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();
    await openRow(wrapper);

    api.getMessages.mockResolvedValue(
      datedPage([dated("a", "2026-09-10")], {
        hasNext: true,
        nextCursor: "after-a",
      }),
    );
    row(wrapper).vm.$emit("date-picked", "2026-09-10");
    await flushPromises();
    // The landing scroll settles before the sentinels wake.
    await vi.advanceTimersByTimeAsync(650);

    const sentinels = wrapper.findAll(".chat-feed__sentinel");
    const bottom = sentinels[sentinels.length - 1].element;
    const watching = observers.filter((o) => o.targets.includes(bottom));
    expect(watching.length).toBeGreaterThan(0);

    api.getMessagesAfter.mockResolvedValue(
      datedPage([dated("b", "2026-09-11")]),
    );
    watching
      .at(-1)!
      .callback(
        [{ isIntersecting: true } as IntersectionObserverEntry],
        {} as IntersectionObserver,
      );
    await flushPromises();

    expect(api.getMessagesAfter).toHaveBeenCalledWith(
      "c1",
      "after-a",
      expect.any(Number),
    );
    expect(shownIds()).toEqual(["a", "b"]);
    vi.useRealTimers();
  });
});
