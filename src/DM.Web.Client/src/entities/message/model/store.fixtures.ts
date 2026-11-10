import { mockGetChat, mockGetMessages } from "./store.doubles";
import { useMessagingStore } from "./store";
import type { Chat } from "./types";
import type { ChatType } from "@/shared/api/models/common";
import { asServed } from "@/shared/api/models";

export const message = (id: number) => ({
  id: `m${id}`,
  text: `text ${id}`,
  createdUtc: new Date(2026, 2, 14, 12, 0, id % 60).toISOString(),
});

export const page = (ids: number[], paging: Record<string, unknown> = {}) => ({
  data: {
    resources: ids.map(message),
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

export const createMockChat = (id: string, type: ChatType): Chat => ({
  id: asServed(id),
  type: asServed(type),
  participants: asServed([]),
  unreadMessagesCount: asServed(0),
});

/** Opens a chat sitting on a page that has older history behind it. */
export async function openChatWithHistory() {
  mockGetChat.mockResolvedValue({ data: { id: "c1" }, error: null });
  mockGetMessages.mockResolvedValue(
    page([2, 3], { prevCursor: "cursor-1", hasPrev: true }),
  );

  const store = useMessagingStore();
  await store.selectChat("c1" as never);
  await store.fetchMessages("c1" as never);
  return store;
}
