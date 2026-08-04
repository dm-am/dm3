/**
 * @vitest-environment jsdom
 */

/**
 * The search field stands above the global chat, so it searches the global
 * chat. It used to default to "Везде" and send no scope at all, which made the
 * chat's own box the site's only entry into private correspondence and game
 * posts. It also carried a "По дате | Лучшее совпадение" toggle for a
 * parameter the endpoint does not accept, so both halves returned the same
 * list by date.
 *
 * These pin what the store may ask for: one scope, sent on every page, and not
 * a word about ordering.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";

const { mockSearchMessages } = vi.hoisted(() => ({
  mockSearchMessages: vi.fn(),
}));

vi.mock("../api/messageSearchApi", () => ({
  default: { searchMessages: mockSearchMessages },
}));

import { useMessageSearchStore } from "./searchStore";

const hit = (id: string) => ({
  sourceType: "global" as const,
  sourceId: "c1",
  sourceTitle: null,
  id,
  createdUtc: "2026-01-01T00:00:00Z",
  snippet: "hello",
});

const page = (
  resources: ReturnType<typeof hit>[],
  nextCursor: string | null = null,
) => ({
  data: {
    resources,
    paging: {
      nextCursor,
      prevCursor: null,
      hasNext: nextCursor !== null,
      hasPrev: false,
    },
  },
  error: null,
});

/** Params of the n-th call, as the store spelled them. */
const paramsOf = (call: number) =>
  mockSearchMessages.mock.calls[call][0] as Record<string, unknown>;

/** A response the test releases by hand, to keep two requests in flight. */
function deferred() {
  let release!: (value: ReturnType<typeof page>) => void;
  const promise = new Promise<ReturnType<typeof page>>((resolve) => {
    release = resolve;
  });
  return { promise, release };
}

describe("useMessageSearchStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    mockSearchMessages.mockReset();
  });

  it("does not call the API for an empty query", async () => {
    const store = useMessageSearchStore();
    store.query = "   ";
    await store.search();
    expect(mockSearchMessages).not.toHaveBeenCalled();
    expect(store.hasSearched).toBe(false);
  });

  it("searches the global chat and says nothing about ordering", async () => {
    mockSearchMessages.mockResolvedValue(page([hit("m1")], "cur2"));

    const store = useMessageSearchStore();
    store.query = "hello";
    await store.search();

    expect(paramsOf(0)).toEqual({ search: "hello", in: ["global"], limit: 50 });
    // Explicit: an absent key and a key holding undefined compare equal above.
    expect("sort" in paramsOf(0)).toBe(false);
    expect(store.results).toHaveLength(1);
    expect(store.nextCursor).toBe("cur2");
    expect(store.hasMore).toBe(true);
    expect(store.hasSearched).toBe(true);
  });

  it("keeps the scope on the pages after the first", async () => {
    const store = useMessageSearchStore();
    store.query = "hi";
    mockSearchMessages.mockResolvedValueOnce(page([hit("m1")], "cur2"));
    await store.search();

    mockSearchMessages.mockResolvedValueOnce(page([hit("m2")]));
    await store.loadMore();

    expect(paramsOf(1)).toEqual({
      search: "hi",
      in: ["global"],
      cursor: "cur2",
      limit: 50,
    });
    expect("sort" in paramsOf(1)).toBe(false);
    expect(store.results).toHaveLength(2);
    expect(store.hasMore).toBe(false);
    expect(store.nextCursor).toBeNull();
  });

  it("keeps the answer of the newest search when an older one lands later", async () => {
    const slow = deferred();
    const fast = deferred();
    mockSearchMessages
      .mockReturnValueOnce(slow.promise)
      .mockReturnValueOnce(fast.promise);

    const store = useMessageSearchStore();
    store.query = "коб";
    const first = store.search();
    store.query = "кобольд";
    const second = store.search();

    fast.release(page([hit("new")]));
    await second;
    slow.release(page([hit("old")]));
    await first;

    expect(store.results.map((r) => r.id)).toEqual(["new"]);
    expect(store.loading).toBe(false);
  });

  it("does not append a page a newer search has already replaced", async () => {
    mockSearchMessages.mockResolvedValueOnce(page([hit("m1")], "cur2"));
    const store = useMessageSearchStore();
    store.query = "коб";
    await store.search();

    const older = deferred();
    const newer = deferred();
    mockSearchMessages
      .mockReturnValueOnce(older.promise)
      .mockReturnValueOnce(newer.promise);

    const tail = store.loadMore();
    store.query = "кобольд";
    const fresh = store.search();

    newer.release(page([hit("m2")]));
    await fresh;
    older.release(page([hit("m1-page2")], "cur3"));
    await tail;

    expect(store.results.map((r) => r.id)).toEqual(["m2"]);
    expect(store.nextCursor).toBeNull();
  });

  it("surfaces an error and clears results on API failure", async () => {
    mockSearchMessages.mockResolvedValue({
      data: null,
      error: { type: "Server", title: "boom", status: 500, traceId: "" },
    });
    const store = useMessageSearchStore();
    store.query = "x";
    await store.search();
    expect(store.error).toBe("Не удалось выполнить поиск");
    expect(store.results).toHaveLength(0);
  });
});
