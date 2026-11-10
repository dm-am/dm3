/**
 * @vitest-environment jsdom
 */

/**
 * ChatView, the live tail of the correspondence: the newest messages read in
 * on a push, a reconnect and the return to the tab, and the receipt for the
 * ones the reader has seen (useChatLiveTail).
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { nextTick } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { useMessagingStore } from "@/entities/message";
import { NotificationType } from "@/shared/api/models/notifications";
import { api, hub, virtual } from "./ChatView.doubles";
import {
  closeChat,
  feed,
  heldAnswer,
  messagePage,
  openChat,
  resetChatView,
  setVisibility,
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
vi.mock("@/shared/lib/composables/useVirtualScroll", async (original) =>
  (await import("./ChatView.doubles")).virtualScroll(await original()),
);

beforeEach(resetChatView);
afterEach(closeChat);

/**
 * Puts the reader at `scrollTop` of a feed 1000 high, 300 of it in view.
 * jsdom lays nothing out and answers zero everywhere, which reads as "at the
 * end" and cannot tell a feed taken down from one with nowhere to go.
 */
function park(wrapper: ReturnType<typeof mount>, scrollTop: number) {
  const el = feed(wrapper);
  Object.defineProperty(el, "scrollHeight", {
    value: 1000,
    configurable: true,
  });
  Object.defineProperty(el, "clientHeight", { value: 300, configurable: true });
  Object.defineProperty(el, "scrollTop", {
    value: scrollTop,
    writable: true,
    configurable: true,
  });
}

/** The reader well above the end of the correspondence. */
const parkAboveTheTail = (wrapper: ReturnType<typeof mount>) =>
  park(wrapper, 0);

/** The reader at the end, in a feed tall enough to scroll. */
const parkAtTheTail = (wrapper: ReturnType<typeof mount>) => park(wrapper, 700);

/** The reader scrolling back down: the position written on the element, then its scroll event. */
function returnToTheTail(wrapper: ReturnType<typeof mount>) {
  const el = feed(wrapper);
  el.scrollTop = 700;
  el.dispatchEvent(new Event("scroll"));
}

/** The message the API answers a send with. */
function sentMessage(id: string) {
  return {
    data: { id, text: id, createdUtc: "2026-07-01T10:00:02Z", likes: [] },
    error: null,
  };
}

function pushNewMessage(chatId: string) {
  hub.handlers.forEach((handler) =>
    handler({
      id: "n1",
      eventType: NotificationType.NewMessage,
      payload: { chatId, messageId: "m2" },
    }),
  );
}

// The push is only a hint to read (API_DESIGN.md, "Realtime"): the read is
// deduplicated by id, and a tail that does not overlap the screen is a gap,
// read again whole.
describe("ChatView, live tail", () => {
  it("leaves the feed alone on a push about another correspondence", async () => {
    api.getMessages.mockResolvedValueOnce(messagePage(["m1"]));
    await openChat();
    expect(api.getMessages).toHaveBeenCalledTimes(1);

    pushNewMessage("c2");
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalledTimes(1);
    expect(shownIds()).toEqual(["m1"]);
  });

  it("reads the tail on a push about the open one and repeats nothing", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    await openChat();
    // The open one is the correspondence its address names.
    expect(api.getChat.mock.calls[0][0]).toBe("c1");

    pushNewMessage("c1");
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalledTimes(2);
    expect(shownIds()).toEqual(["m1", "m2"]);
  });

  it("re-reads the window whole when the tail does not overlap it", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m9", "m10"]))
      .mockResolvedValueOnce(messagePage(["m8", "m9", "m10"]));
    await openChat();

    pushNewMessage("c1");
    await flushPromises();

    // The third call is the re-read; m1 is gone instead of sitting above a hole.
    expect(api.getMessages).toHaveBeenCalledTimes(3);
    expect(shownIds()).toEqual(["m8", "m9", "m10"]);
  });

  // A gap bigger than a page does not replace the window under a reader up in the history.
  it("leaves the window in place under a reader who has paged up", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m9", "m10"]));
    const wrapper = await openChat();
    parkAboveTheTail(wrapper);
    const el = feed(wrapper);

    pushNewMessage("c1");
    await flushPromises();

    // The tail was read and nothing came of it: the reader is where he was.
    expect(api.getMessages).toHaveBeenCalledTimes(2);
    expect(shownIds()).toEqual(["m1"]);
    expect(el.scrollTop).toBe(0);
  });

  it("re-reads the window when the reader comes back down to the end", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m9", "m10"]))
      .mockResolvedValueOnce(messagePage(["m9", "m10"]))
      .mockResolvedValueOnce(messagePage(["m8", "m9", "m10"]));
    const wrapper = await openChat();
    parkAboveTheTail(wrapper);

    pushNewMessage("c1");
    await flushPromises();
    expect(shownIds()).toEqual(["m1"]);

    returnToTheTail(wrapper);
    await flushPromises();

    // The tail is read again: the deferred answer is as old as his scroll back.
    expect(api.getMessages).toHaveBeenCalledTimes(4);
    expect(shownIds()).toEqual(["m8", "m9", "m10"]);
  });

  it("reads the tail when the tab comes back on screen", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    await openChat();

    document.dispatchEvent(new Event("visibilitychange"));
    await flushPromises();

    expect(shownIds()).toEqual(["m1", "m2"]);
  });

  it("reads the tail when the socket comes back up", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    await openChat();

    hub.connected.value = true;
    await nextTick();
    await flushPromises();

    expect(shownIds()).toEqual(["m1", "m2"]);
  });

  it("stops listening once the correspondence is closed", async () => {
    api.getMessages.mockResolvedValueOnce(messagePage(["m1"]));
    await openChat();

    closeChat();
    await flushPromises();

    expect(hub.handlers.size).toBe(0);
    pushNewMessage("c1");
    await flushPromises();
    expect(api.getMessages).toHaveBeenCalledTimes(1);
  });

  it("lets go of the tab listener once the correspondence is closed", async () => {
    api.getMessages.mockResolvedValueOnce(messagePage(["m1"]));
    await openChat();
    const removeListener = vi.spyOn(document, "removeEventListener");

    closeChat();
    await flushPromises();

    expect(removeListener).toHaveBeenCalledWith(
      "visibilitychange",
      expect.any(Function),
    );
    removeListener.mockRestore();
  });

  it("reads nothing when the tab goes off screen", async () => {
    api.getMessages.mockResolvedValueOnce(messagePage(["m1"]));
    await openChat();

    setVisibility("hidden");
    document.dispatchEvent(new Event("visibilitychange"));
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalledTimes(1);
  });

  it("reads no tail for a reader standing in the middle of the history", async () => {
    // hasNext: the window ends before the newest message.
    api.getMessages.mockResolvedValueOnce(
      messagePage(["m1"], { hasNext: true }),
    );
    await openChat();

    pushNewMessage("c1");
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalledTimes(1);
    expect(shownIds()).toEqual(["m1"]);
  });

  it("drops a tail that landed after the reader left the end of the history", async () => {
    const held = heldAnswer<ReturnType<typeof messagePage>>();
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockReturnValueOnce(held.answer)
      .mockResolvedValueOnce(messagePage(["m8", "m9", "m10"]));
    await openChat();

    pushNewMessage("c1");
    await flushPromises();
    // The reader landed in search or paged up while the tail was in flight.
    useMessagingStore().hasMoreAfter = true;
    held.settle(messagePage(["m9", "m10"]));
    await flushPromises();

    // No third request: a full re-read would take the reader to the end.
    expect(api.getMessages).toHaveBeenCalledTimes(2);
    expect(shownIds()).toEqual(["m1"]);
  });

  it("puts the overlap in the order the server holds it", async () => {
    api.sendMessage.mockResolvedValue(sentMessage("mine"));
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "theirs", "mine"]));
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("привет");
    await wrapper.find(".chat-composer__send").trigger("click");
    await flushPromises();
    // The store appends the sent message as soon as the POST answers.
    expect(shownIds()).toEqual(["m1", "mine"]);

    pushNewMessage("c1");
    await flushPromises();

    expect(shownIds()).toEqual(["m1", "theirs", "mine"]);
  });

  it("waits for a send in flight and then reads the tail once", async () => {
    const held = heldAnswer<ReturnType<typeof sentMessage>>();
    api.sendMessage.mockReturnValue(held.answer);
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "mine"]));
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("привет");
    await wrapper.find(".chat-composer__send").trigger("click");
    await flushPromises();

    pushNewMessage("c1");
    await flushPromises();
    // Reading now would bring the sent message in ahead of the store.
    expect(api.getMessages).toHaveBeenCalledTimes(1);

    held.settle(sentMessage("mine"));
    await flushPromises();

    expect(api.getMessages).toHaveBeenCalledTimes(2);
    expect(shownIds()).toEqual(["m1", "mine"]);
  });
});

// The header badge and the open correspondence answer the same push.
describe("ChatView, read receipt for the live tail", () => {
  it("marks the correspondence read when a message arrives in a visible tab", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    await openChat();
    // Opening marks it read too; the receipt counted is for the later message.
    api.markAsRead.mockClear();

    pushNewMessage("c1");
    await flushPromises();

    expect(shownIds()).toEqual(["m1", "m2"]);
    expect(api.markAsRead).toHaveBeenCalledTimes(1);
    expect(api.markAsRead).toHaveBeenCalledWith("c1");
  });

  // One measurement before the list grows decides both the scroll and the receipt.
  it("takes the feed down to the message and marks it read for a reader at the end", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    const wrapper = await openChat();
    api.markAsRead.mockClear();
    parkAtTheTail(wrapper);
    virtual.scrollToIndex.mockClear();

    pushNewMessage("c1");
    await flushPromises();
    await nextTick();

    expect(shownIds()).toEqual(["m1", "m2"]);
    // The date and the two messages: the last row is the third.
    expect(virtual.scrollToIndex).toHaveBeenLastCalledWith(2, {
      align: "end",
    });
    expect(api.markAsRead).toHaveBeenCalledTimes(1);
  });

  it("moves neither the reader nor the unread mark above the end of the history", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    const wrapper = await openChat();
    api.markAsRead.mockClear();
    parkAboveTheTail(wrapper);
    const el = feed(wrapper);
    virtual.scrollToIndex.mockClear();

    pushNewMessage("c1");
    await flushPromises();
    await nextTick();

    // Below the fold: the unread mark is the one sign it arrived.
    expect(shownIds()).toEqual(["m1", "m2"]);
    expect(el.scrollTop).toBe(0);
    expect(virtual.scrollToIndex).not.toHaveBeenCalled();
    expect(api.markAsRead).not.toHaveBeenCalled();
  });

  it("holds the receipt while the tab is hidden and sends it on the way back", async () => {
    api.getMessages
      .mockResolvedValueOnce(messagePage(["m1"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]))
      .mockResolvedValueOnce(messagePage(["m1", "m2"]));
    await openChat();
    api.markAsRead.mockClear();
    setVisibility("hidden");

    pushNewMessage("c1");
    await flushPromises();

    // In the feed, and nobody has looked at it yet.
    expect(shownIds()).toEqual(["m1", "m2"]);
    expect(api.markAsRead).not.toHaveBeenCalled();

    setVisibility("visible");
    document.dispatchEvent(new Event("visibilitychange"));
    await flushPromises();

    // The return to the tab reads the tail, and the receipt goes with it.
    expect(api.getMessages).toHaveBeenCalledTimes(3);
    expect(api.markAsRead).toHaveBeenCalledTimes(1);
    expect(api.markAsRead).toHaveBeenCalledWith("c1");
  });
});
