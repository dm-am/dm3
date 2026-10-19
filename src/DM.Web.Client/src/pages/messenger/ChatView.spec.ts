/**
 * @vitest-environment jsdom
 */

/**
 * ChatView, the private correspondence: drawn as the global chat is, the feed
 * and its toolbar, the composer and the field by the server's answer, and the
 * link of a message. The search and the calendar, the live tail and the menu
 * of the correspondence have specs of their own beside this one.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { nextTick } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { useToast } from "@/shared/lib/composables/useToast";
import { api, clearSpy, nav, toolbar } from "./ChatView.doubles";
import {
  closeChat,
  heldAnswer,
  messagePage,
  openChat,
  resetChatView,
  shownIds,
} from "./ChatView.fixtures";
import { problem } from "@/shared/lib/testing/problem";

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
vi.mock("@/shared/lib/composables/useMessageToolbar", async (original) =>
  (await import("./ChatView.doubles")).messageToolbar(await original()),
);

beforeEach(resetChatView);
afterEach(closeChat);

const field = (wrapper: ReturnType<typeof mount>) =>
  wrapper.find(".editor-stub").element as HTMLTextAreaElement;

// A private message does not render [mod], so the composer offers no button for it.
describe("ChatView, the tag set of a private message", () => {
  it("gives the composer the tags of a private message", async () => {
    const wrapper = await openChat();
    expect(
      wrapper.findComponent({ name: "BBCodeEditor" }).props("context"),
    ).toBe("direct");
  });
});

// The page mounts before the correspondence loads; the feed's markup is set up when it exists.
describe("ChatView, the markup of the feed", () => {
  it("is set up only once the feed is on screen", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const nullContainer = () =>
      warn.mock.calls.filter((args) =>
        String(args[0]).includes("null container"),
      );
    // Held open, so the page mounts with no feed yet, as over a real connection.
    const chat = heldAnswer<unknown>();
    api.getChat.mockReturnValue(chat.answer);
    api.getMessages.mockResolvedValue(messagePage(["m1", "m2"]));
    try {
      const wrapper = await openChat();
      expect(wrapper.find(".chat-message-skeleton-list").exists()).toBe(true);
      expect(nullContainer()).toEqual([]);

      chat.settle({ data: { id: "c1", participants: [] }, error: null });
      await flushPromises();
      await nextTick();

      expect(wrapper.find(".chat-message-skeleton-list").exists()).toBe(false);
      expect(nullContainer()).toEqual([]);
    } finally {
      warn.mockRestore();
    }
  });
});

// The global chat with another line at the top of the frame; no heading of its
// own, no "← Назад", the feed's words for every state.
describe("ChatView, the correspondence in the global chat's dress", () => {
  const withBob = {
    data: {
      id: "c1",
      participants: [{ username: "Alice" }, { username: "Bob" }],
    },
    error: null,
  };

  it("leaves the one heading to the section", async () => {
    api.getChat.mockResolvedValue(withBob);
    const wrapper = await openChat();

    expect(wrapper.find("page-title").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Назад");
  });

  it("names the other one, his status, the search and the menu at the top of the frame", async () => {
    api.getChat.mockResolvedValue(withBob);
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();

    const strip = wrapper.get(".chat-strip");
    expect(strip.text().replace(/\s+/g, " ")).toBe("Bob [offline] | поиск | …");
    const name = strip.get("a.line-name");
    expect(JSON.parse(name.attributes("data-to")!)).toEqual({
      name: "profile",
      params: { username: "Bob" },
    });
    expect(strip.find(".chat-search-row").exists()).toBe(false);
  });

  it("says the other one is online while he is", async () => {
    api.getChat.mockResolvedValue({
      data: {
        id: "c1",
        participants: [
          { username: "Alice" },
          { username: "Bob", lastActivityUtc: new Date().toISOString() },
        ],
      },
      error: null,
    });
    const wrapper = await openChat();

    expect(wrapper.get(".chat-strip .online").text()).toBe("online");
    expect(wrapper.find(".chat-strip .offline").exists()).toBe(false);
  });

  it("names a group by its title and the number of its participants", async () => {
    api.getChat.mockResolvedValue({
      data: {
        id: "c1",
        type: "Group",
        title: "Партия",
        participants: [
          { username: "Alice" },
          { username: "Bob" },
          { username: "Eve" },
        ],
        viewer: { canWrite: true, canLeave: true, canClearHistory: true },
      },
      error: null,
    });
    const wrapper = await openChat();

    const strip = wrapper.get(".chat-strip");
    expect(strip.text().replace(/\s+/g, " ")).toBe("Партия, участников: 3 | …");
    expect(strip.find("a.line-name").exists()).toBe(false);
  });

  it("says neither missing nor empty while the correspondence is on its way", async () => {
    const chat = heldAnswer<unknown>();
    api.getChat.mockReturnValue(chat.answer);
    const wrapper = await openChat();

    expect(wrapper.text()).not.toContain("Переписка не найдена");
    expect(wrapper.text()).not.toContain("Сообщений пока нет");
    expect(wrapper.find(".chat-message-skeleton-list").exists()).toBe(true);
    chat.settle(withBob);
  });

  it("says neither while the first window is on its way", async () => {
    const window = heldAnswer<unknown>();
    api.getMessages.mockReturnValue(window.answer);
    const wrapper = await openChat();

    expect(wrapper.text()).not.toContain("Переписка не найдена");
    expect(wrapper.text()).not.toContain("Сообщений пока нет");
    expect(wrapper.find(".chat-message-skeleton-list").exists()).toBe(true);
    window.settle(messagePage([]));
  });

  it("says it is empty once the server says so", async () => {
    const wrapper = await openChat();

    expect(wrapper.text()).toContain("Сообщений пока нет");
  });

  it("calls it missing on the server's answer, with the way to the others", async () => {
    api.getChat.mockResolvedValue({ data: null, error: problem(404) });
    const wrapper = await openChat();

    expect(wrapper.text()).toContain("Переписка не найдена");
    const back = wrapper.get("a");
    expect(back.text()).toBe("Все переписки");
    // The list beside it stays on its page; the address here names none.
    expect(JSON.parse(back.attributes("data-to")!)).toEqual({
      name: "messenger",
      query: {},
    });
    expect(wrapper.find(".chat-feed__list").exists()).toBe(false);
  });

  it("offers a retry when the correspondence did not come", async () => {
    api.getChat.mockResolvedValueOnce({ data: null, error: problem(500) });
    const wrapper = await openChat();

    expect(wrapper.text()).not.toContain("Переписка не найдена");
    expect(wrapper.get(".error-state").text()).toContain(
      "Не удалось загрузить переписку",
    );

    api.getChat.mockResolvedValue(withBob);
    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(api.getChat).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".error-state").exists()).toBe(false);
    expect(wrapper.get(".chat-strip").text()).toContain("Bob");
  });

  it("holds a bar where the name will be while the correspondence is on its way", async () => {
    const chat = heldAnswer<unknown>();
    api.getChat.mockReturnValue(chat.answer);
    const wrapper = await openChat();

    expect(wrapper.find(".chat-strip .line-placeholder").exists()).toBe(true);
    expect(wrapper.get(".chat-strip").text()).toBe("");
    chat.settle(withBob);
  });

  it("offers a retry when its messages did not come, and reads none of them", async () => {
    api.getMessages.mockResolvedValueOnce({
      data: null,
      error: problem(500),
    });
    const wrapper = await openChat();

    expect(wrapper.text()).not.toContain("Сообщений пока нет");
    expect(wrapper.get(".error-state").text()).toContain(
      "Не удалось загрузить сообщения",
    );
    expect(api.markAsRead).not.toHaveBeenCalled();

    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(shownIds()).toEqual(["m1"]);
    expect(api.markAsRead).toHaveBeenCalledWith("c1");
  });
});

// The toolbar goes away the moment the feed scrolls, the handlers set once the feed exists.
describe("ChatView, scrolling the feed", () => {
  async function openLate() {
    const chat = heldAnswer<unknown>();
    api.getChat.mockReturnValue(chat.answer);
    api.getMessages.mockResolvedValue(messagePage(["m1", "m2"]));
    const wrapper = await openChat();
    chat.settle({ data: { id: "c1", participants: [] }, error: null });
    await flushPromises();
    await nextTick();
    return wrapper;
  }

  it("takes the toolbar away on a scroll of a feed that loaded after the page", async () => {
    const wrapper = await openLate();
    toolbar.last!.hoveredMessageId.value = "m1";

    await wrapper.get(".chat-feed__list").trigger("scroll");

    expect(toolbar.last!.hoveredMessageId.value).toBeNull();
  });

  it("takes the toolbar away on a turn of the wheel over it as well", async () => {
    const wrapper = await openLate();
    toolbar.last!.hoveredMessageId.value = "m1";

    await wrapper.get(".chat-feed__list").trigger("wheel");

    expect(toolbar.last!.hoveredMessageId.value).toBeNull();
  });
});

// A refused send hands everything back: the field empties at once, and the
// text, the draft and a sentence return when the send does not land.
describe("ChatView, sending a message", () => {
  it("keeps the text, the draft and names the reason when the send fails", async () => {
    api.sendMessage.mockResolvedValue({ data: null, error: problem(400) });
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("длинное письмо");
    await wrapper.find(".chat-composer__send").trigger("click");
    await flushPromises();
    await nextTick();

    expect(api.sendMessage).toHaveBeenCalledWith("c1", "длинное письмо");
    expect(field(wrapper).value).toBe("длинное письмо");
    // clear() drops the saved draft, the only copy that outlives the tab.
    expect(clearSpy).not.toHaveBeenCalled();
    // Under the "Отправить" that sent it, and in no plate.
    expect(wrapper.get(".chat-composer__send + .action-result").text()).toBe(
      "Не удалось отправить сообщение",
    );
    expect(useToast().toasts.value.map((t) => t.message)).toEqual([]);
  });

  it("empties the field and drops the draft once the send lands", async () => {
    api.sendMessage.mockResolvedValue({
      data: {
        id: "m1",
        text: "привет",
        createdUtc: "2026-07-01T10:00:00Z",
        likes: [],
      },
      error: null,
    });
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("привет");
    await wrapper.find(".chat-composer__send").trigger("click");
    await flushPromises();
    await nextTick();

    expect(field(wrapper).value).toBe("");
    expect(clearSpy).toHaveBeenCalledTimes(1);
    expect(useToast().toasts.value).toHaveLength(0);
  });

  it("sends nothing on a field holding only spaces", async () => {
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("   ");
    // Through the editor's own submit (Ctrl+Enter): the guard is in the handler.
    wrapper.findComponent({ name: "BBCodeEditor" }).vm.$emit("submit");
    await flushPromises();

    expect(api.sendMessage).not.toHaveBeenCalled();
    expect(field(wrapper).value).toBe("   ");
    expect(useToast().toasts.value).toHaveLength(0);
  });
});

// The field stands where the server says the message would be taken
// (viewer.canWrite): the other side may keep the reader on a blacklist.
describe("ChatView, the field by the server's answer", () => {
  it("has no field where the server says the message would be refused", async () => {
    api.getChat.mockResolvedValue({
      data: { id: "c1", participants: [], viewer: { canWrite: false } },
      error: null,
    });
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();

    expect(wrapper.find(".chat-feed__list").exists()).toBe(true);
    expect(wrapper.findComponent({ name: "ChatComposer" }).exists()).toBe(
      false,
    );
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    // Nothing stands in its place.
    expect(wrapper.get(".chat-feed__composer").text()).toBe("");
  });

  it("has no field on an answer that says nothing about the reader", async () => {
    api.getChat.mockResolvedValue({
      data: { id: "c1", participants: [] },
      error: null,
    });
    const wrapper = await openChat();

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
  });

  it("offers no field while another correspondence is on its way", async () => {
    const wrapper = await openChat();
    expect(wrapper.find(".editor-stub").exists()).toBe(true);

    const next = heldAnswer<unknown>();
    api.getChat.mockReturnValueOnce(next.answer);
    nav.route.params = { id: "c2" };
    await flushPromises();

    // The answer about the correspondence left behind is not this one's.
    expect(wrapper.find(".editor-stub").exists()).toBe(false);

    next.settle({
      data: { id: "c2", participants: [], viewer: { canWrite: true } },
      error: null,
    });
    await flushPromises();

    expect(wrapper.find(".editor-stub").exists()).toBe(true);
  });
});

// The link of a message opens the correspondence on it: an ordinary link.
describe("ChatView, the link of a message", () => {
  it("leads to the correspondence on the message", async () => {
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();

    const linkTo = wrapper
      .findComponent({ name: "ChatFeed" })
      .props("linkTo") as (id: string) => unknown;
    expect(linkTo("m1")).toEqual({
      name: "chat",
      params: { id: "c1" },
      query: { msg: "m1" },
    });
  });

  it("keeps the page of the list beside the correspondence", async () => {
    nav.route.query = { number: "2" };
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    const wrapper = await openChat();

    const linkTo = wrapper
      .findComponent({ name: "ChatFeed" })
      .props("linkTo") as (id: string) => unknown;
    expect(linkTo("m1")).toEqual({
      name: "chat",
      params: { id: "c1" },
      query: { number: "2", msg: "m1" },
    });
  });
});
