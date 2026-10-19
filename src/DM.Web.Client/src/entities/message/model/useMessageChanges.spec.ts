/**
 * @vitest-environment jsdom
 */

/**
 * The reader's changes to the messages of a correspondence, through
 * useMessagingStore (useMessageChanges), and the answers that land out of
 * order.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import { mockGetChat, mockGetMessages, mockSendMessage } from "./store.doubles";
import { message, openChatWithHistory } from "./store.fixtures";

vi.mock("../api/messagingApi", async () =>
  (await import("./store.doubles")).messagingApi(),
);

import { useMessagingStore } from "./store";
import { deferred } from "@/shared/lib/testing/tasks";

describe("useMessagingStore, sending a message", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("hands the refusal back and adds nothing to the list", async () => {
    const store = await openChatWithHistory();
    mockSendMessage.mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 400, traceId: "t" },
    });

    const { error } = await store.sendMessage("c1" as never, "письмо");

    // The page restores what was typed from this. While the answer was the
    // created message or null, a refusal and a success were the same value,
    // and the page emptied the composer either way.
    expect(error?.status).toBe(400);
    expect(store.messagesList.map((m) => m.id)).toEqual(["m2", "m3"]);
  });

  it("appends the sent message and points the chat at it", async () => {
    const store = await openChatWithHistory();
    mockSendMessage.mockResolvedValue({ data: message(4), error: null });

    const { error } = await store.sendMessage("c1" as never, "письмо");

    expect(error).toBeNull();
    expect(store.messagesList.map((m) => m.id)).toEqual(["m2", "m3", "m4"]);
    expect(store.selectedChat?.lastMessage?.id).toBe("m4");
  });

  describe("answers out of order", () => {
    // The answer for A arriving after B's must not show the chat the reader left.
    it("keeps the correspondence chosen last", async () => {
      const older = deferred<unknown>();
      const newer = deferred<unknown>();
      mockGetChat
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);

      const store = useMessagingStore();
      const first = store.selectChat("a" as never);
      const second = store.selectChat("b" as never);

      newer.resolve({ data: { id: "b" }, error: null });
      await second;
      older.resolve({ data: { id: "a" }, error: null });
      await first;

      expect(store.selectedChat?.id).toBe("b");
    });

    it("keeps the window of messages asked for last", async () => {
      const older = deferred<unknown>();
      const newer = deferred<unknown>();
      mockGetMessages
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);

      const store = useMessagingStore();
      const first = store.fetchMessages("a" as never);
      const second = store.fetchMessages("b" as never);

      newer.resolve({
        data: { resources: [{ id: "mb" }], paging: null },
        error: null,
      });
      await second;
      older.resolve({
        data: { resources: [{ id: "ma" }], paging: null },
        error: null,
      });
      await first;

      expect(store.messagesList.map((m) => m.id)).toEqual(["mb"]);
      expect(store.loadingMessages).toBe(false);
    });
  });
});
