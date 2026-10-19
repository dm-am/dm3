/**
 * @vitest-environment jsdom
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
import { deferred } from "@/shared/lib/testing/tasks";

const hit = (id: string) => ({
  id,
  createdUtc: "2026-01-01T00:00:00Z",
  snippet: [{ text: "hello", isMatch: false }],
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

const paramsOf = (call: number) =>
  mockSearchMessages.mock.calls[call][0] as Record<string, unknown>;

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
    const slow = deferred<ReturnType<typeof page>>();
    const fast = deferred<ReturnType<typeof page>>();
    mockSearchMessages
      .mockReturnValueOnce(slow.promise)
      .mockReturnValueOnce(fast.promise);

    const store = useMessageSearchStore();
    store.query = "коб";
    const first = store.search();
    store.query = "кобольд";
    const second = store.search();

    fast.resolve(page([hit("new")]));
    await second;
    slow.resolve(page([hit("old")]));
    await first;

    expect(store.results.map((r) => r.id)).toEqual(["new"]);
    expect(store.loading).toBe(false);
  });

  it("does not append a page a newer search has already replaced", async () => {
    mockSearchMessages.mockResolvedValueOnce(page([hit("m1")], "cur2"));
    const store = useMessageSearchStore();
    store.query = "коб";
    await store.search();

    const older = deferred<ReturnType<typeof page>>();
    const newer = deferred<ReturnType<typeof page>>();
    mockSearchMessages
      .mockReturnValueOnce(older.promise)
      .mockReturnValueOnce(newer.promise);

    const tail = store.loadMore();
    store.query = "кобольд";
    const fresh = store.search();

    newer.resolve(page([hit("m2")]));
    await fresh;
    older.resolve(page([hit("m1-page2")], "cur3"));
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

  it("searches the one correspondence it is pointed at", async () => {
    mockSearchMessages.mockResolvedValue(page([hit("m1")], "cur2"));
    const store = useMessageSearchStore();
    store.setScope("dm:c7");
    store.query = "привет";

    await store.search();
    await store.loadMore();

    expect(paramsOf(0)).toEqual({
      search: "привет",
      in: ["dm:c7"],
      limit: 50,
    });
    expect(paramsOf(1).in).toEqual(["dm:c7"]);
  });

  it("forgets the query and the hits of another feed", async () => {
    mockSearchMessages.mockResolvedValue(page([hit("m1")]));
    const store = useMessageSearchStore();
    store.query = "привет";
    await store.search();

    store.setScope("dm:c7");

    expect(store.query).toBe("");
    expect(store.results).toEqual([]);
    expect(store.hasSearched).toBe(false);
  });

  it("drops an answer about the feed it was pointed away from", async () => {
    const slow = deferred<ReturnType<typeof page>>();
    mockSearchMessages.mockReturnValueOnce(slow.promise);
    const store = useMessageSearchStore();
    store.query = "привет";
    const pending = store.search();

    store.setScope("dm:c7");
    slow.resolve(page([hit("m1")]));
    await pending;

    expect(store.results).toEqual([]);
  });

  it("keeps the query when it is pointed at the feed it is on", async () => {
    mockSearchMessages.mockResolvedValue(page([hit("m1")]));
    const store = useMessageSearchStore();
    store.setScope("dm:c7");
    store.query = "привет";
    await store.search();

    store.setScope("dm:c7");

    expect(store.query).toBe("привет");
    expect(store.results).toHaveLength(1);
  });
});
