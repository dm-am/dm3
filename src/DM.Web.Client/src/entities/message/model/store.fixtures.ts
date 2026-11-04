import { mockGetChat, mockGetMessages } from "./store.doubles";
import { useMessagingStore } from "./store";

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
