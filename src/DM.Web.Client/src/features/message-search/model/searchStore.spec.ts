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

import {
  useMessageSearchStore,
  scopeToIn,
  sortToParam,
  scopeNeedsTarget,
} from "./searchStore";

describe("scopeToIn", () => {
  it("maps all -> undefined (search everything)", () => {
    expect(scopeToIn({ kind: "all" })).toBeUndefined();
  });

  it("maps global -> ['global']", () => {
    expect(scopeToIn({ kind: "global" })).toEqual(["global"]);
  });

  it("maps dm with a target -> ['dm:<id>']", () => {
    expect(scopeToIn({ kind: "dm", targetId: "abc" })).toEqual(["dm:abc"]);
  });

  it("maps game with a target -> ['game:<id>']", () => {
    expect(scopeToIn({ kind: "game", targetId: "g1" })).toEqual(["game:g1"]);
  });

  it("returns undefined for dm/game without a target", () => {
    expect(scopeToIn({ kind: "dm" })).toBeUndefined();
    expect(scopeToIn({ kind: "game" })).toBeUndefined();
  });
});

describe("sortToParam", () => {
  it("maps date -> newest and best -> best", () => {
    expect(sortToParam("date")).toBe("newest");
    expect(sortToParam("best")).toBe("best");
  });
});

describe("scopeNeedsTarget", () => {
  it("is true only for dm/game without a target", () => {
    expect(scopeNeedsTarget({ kind: "all" })).toBe(false);
    expect(scopeNeedsTarget({ kind: "global" })).toBe(false);
    expect(scopeNeedsTarget({ kind: "dm" })).toBe(true);
    expect(scopeNeedsTarget({ kind: "game" })).toBe(true);
    expect(scopeNeedsTarget({ kind: "dm", targetId: "x" })).toBe(false);
  });
});

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

  it("populates results and cursor on a successful search", async () => {
    mockSearchMessages.mockResolvedValue({
      data: {
        resources: [
          {
            sourceType: "global",
            sourceId: "c1",
            sourceTitle: null,
            id: "m1",
            createdUtc: "2026-01-01T00:00:00Z",
            snippet: "hello",
          },
        ],
        paging: {
          nextCursor: "cur2",
          prevCursor: null,
          hasNext: true,
          hasPrev: false,
        },
      },
      error: null,
    });

    const store = useMessageSearchStore();
    store.query = "hello";
    await store.search();

    expect(mockSearchMessages).toHaveBeenCalledWith({
      q: "hello",
      in: undefined,
      sort: "newest",
      limit: 50,
    });
    expect(store.results).toHaveLength(1);
    expect(store.nextCursor).toBe("cur2");
    expect(store.hasMore).toBe(true);
    expect(store.hasSearched).toBe(true);
  });

  it("appends the next page on loadMore", async () => {
    const store = useMessageSearchStore();
    store.query = "hi";
    mockSearchMessages.mockResolvedValueOnce({
      data: {
        resources: [
          {
            sourceType: "global",
            sourceId: "c1",
            sourceTitle: null,
            id: "m1",
            createdUtc: "2026-01-02T00:00:00Z",
            snippet: "a",
          },
        ],
        paging: {
          nextCursor: "cur2",
          prevCursor: null,
          hasNext: true,
          hasPrev: false,
        },
      },
      error: null,
    });
    await store.search();

    mockSearchMessages.mockResolvedValueOnce({
      data: {
        resources: [
          {
            sourceType: "game",
            sourceId: "g1",
            sourceTitle: "Игра",
            id: "p1",
            createdUtc: "2026-01-01T00:00:00Z",
            snippet: "b",
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
    await store.loadMore();

    expect(store.results).toHaveLength(2);
    expect(store.hasMore).toBe(false);
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
