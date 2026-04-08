/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { Poll, PollId, PollOption, PollOptionId } from "./types";
import { PollStatus } from "./types";
import type { Served } from "@/shared/api/models";

// Helper to cast raw values to Served type for test mocks
function asServed<T>(value: T): Served<T> {
  return value as Served<T>;
}

// Helper to create a poll option with proper Served types
function createPollOption(id: string, text: string, votesCount: number, voted: boolean | null): PollOption {
  return {
    id: asServed(id as PollOptionId),
    text,
    votesCount: asServed(votesCount),
    voted: asServed(voted),
    voters: null,
    totalVoters: null,
  };
}

// Use vi.hoisted to ensure mocks are created before vi.mock hoisting
const { mockGetPolls, mockGetActivePolls, mockPostPollVote, mockDeletePollVote, mockPostPoll, mockPatchPoll } = vi.hoisted(() => ({
  mockGetPolls: vi.fn(),
  mockGetActivePolls: vi.fn(),
  mockPostPollVote: vi.fn(),
  mockDeletePollVote: vi.fn(),
  mockPostPoll: vi.fn(),
  mockPatchPoll: vi.fn(),
}));

vi.mock("../api/pollApi", () => ({
  default: {
    getPolls: mockGetPolls,
    getActivePolls: mockGetActivePolls,
    postPollVote: mockPostPollVote,
    deletePollVote: mockDeletePollVote,
    postPoll: mockPostPoll,
    patchPoll: mockPatchPoll,
  },
}));

import { usePollsStore } from "./store";

const createMockPoll = (id: string, title: string): Poll => ({
  id: asServed(id as PollId),
  title,
  details: null,
  startsUtc: "2025-01-01T00:00:00Z",
  endsUtc: "2025-12-31T23:59:59Z",
  status: asServed(PollStatus.Active),
  isAnonymous: true,
  options: [
    createPollOption("opt-1", "Option 1", 5, false),
    createPollOption("opt-2", "Option 2", 3, false),
  ],
});

describe("usePollsStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ============================================================================
  // INITIALIZATION
  // ============================================================================

  describe("Initialization", () => {
    it("creates store with initial state", () => {
      const store = usePollsStore();

      expect(store.activePolls).toBeNull();
      expect(store.polls).toBeNull();
    });

    it("has fetch functions", () => {
      const store = usePollsStore();

      expect(typeof store.fetchActivePolls).toBe("function");
      expect(typeof store.fetchPolls).toBe("function");
    });

    it("has mutation functions", () => {
      const store = usePollsStore();

      expect(typeof store.vote).toBe("function");
      expect(typeof store.unvote).toBe("function");
      expect(typeof store.createPoll).toBe("function");
      expect(typeof store.editPoll).toBe("function");
    });
  });

  // ============================================================================
  // FETCH ACTIVE POLLS
  // ============================================================================

  describe("fetchActivePolls", () => {
    it("fetches active polls", async () => {
      const mockPolls = [createMockPoll("1", "Poll 1"), createMockPoll("2", "Poll 2")];
      mockGetActivePolls.mockResolvedValue({
        data: { resources: mockPolls, paging: null },
        error: null,
      });

      const store = usePollsStore();
      await store.fetchActivePolls();

      expect(mockGetActivePolls).toHaveBeenCalled();
    });

    it("updates activePolls on success", async () => {
      const mockPolls = [createMockPoll("1", "Poll 1")];
      mockGetActivePolls.mockResolvedValue({
        data: { resources: mockPolls, paging: null },
        error: null,
      });

      const store = usePollsStore();
      await store.fetchActivePolls();

      expect(store.activePolls).toEqual(mockPolls);
    });

    it("handles fetch error", async () => {
      mockGetActivePolls.mockResolvedValue({
        data: null,
        error: { status: 500, title: "Server error" },
      });

      const store = usePollsStore();
      await store.fetchActivePolls();

      expect(store.activePollsError).toBeTruthy();
    });
  });

  // ============================================================================
  // FETCH POLLS (PAGINATED)
  // ============================================================================

  describe("fetchPolls", () => {
    it("fetches polls with params", async () => {
      mockGetPolls.mockResolvedValue({
        data: { resources: [], paging: { current: 1, pages: 1, total: 0 } },
        error: null,
      });

      const store = usePollsStore();
      await store.fetchPolls({ number: 2, sortBy: "starts" });

      expect(mockGetPolls).toHaveBeenCalledWith({ number: 2, sortBy: "starts" });
    });

    it("updates polls on success", async () => {
      const mockData = {
        resources: [createMockPoll("1", "Poll")],
        paging: { current: 1, pages: 1, total: 1 },
      };
      mockGetPolls.mockResolvedValue({ data: mockData, error: null });

      const store = usePollsStore();
      await store.fetchPolls({ number: 1 });

      expect(store.polls).toEqual(mockData);
    });

    it("can filter by status", async () => {
      mockGetPolls.mockResolvedValue({
        data: { resources: [], paging: null },
        error: null,
      });

      const store = usePollsStore();
      await store.fetchPolls({ status: PollStatus.Active });

      expect(mockGetPolls).toHaveBeenCalledWith({ status: PollStatus.Active });
    });
  });

  // ============================================================================
  // VOTING
  // ============================================================================

  describe("vote", () => {
    it("calls vote API with correct params", async () => {
      const updatedPoll = createMockPoll("poll-1", "Poll");
      updatedPoll.options[0].voted = asServed<boolean | null>(true);
      updatedPoll.options[0].votesCount = asServed(6);

      mockPostPollVote.mockResolvedValue({ data: updatedPoll, error: null });

      const store = usePollsStore();
      await store.vote(asServed("poll-1" as PollId), asServed("opt-1" as PollOptionId));

      expect(mockPostPollVote).toHaveBeenCalledWith(asServed("poll-1" as PollId), asServed("opt-1" as PollOptionId));
    });

    it("updates poll in active polls after voting", async () => {
      const poll = createMockPoll("poll-1", "Poll");
      mockGetActivePolls.mockResolvedValue({
        data: { resources: [poll], paging: null },
        error: null,
      });

      const store = usePollsStore();
      await store.fetchActivePolls();

      const updatedPoll = { ...poll, options: [{ ...poll.options[0], voted: asServed<boolean | null>(true), votesCount: asServed(6) }, poll.options[1]] };
      mockPostPollVote.mockResolvedValue({ data: updatedPoll, error: null });

      await store.vote(asServed("poll-1" as PollId), asServed("opt-1" as PollOptionId));

      expect(store.activePolls?.[0].options[0].voted).toBe(asServed<boolean | null>(true));
    });
  });

  describe("unvote", () => {
    it("calls unvote API with correct params", async () => {
      const updatedPoll = createMockPoll("poll-1", "Poll");
      mockDeletePollVote.mockResolvedValue({ data: updatedPoll, error: null });

      const store = usePollsStore();
      await store.unvote(asServed("poll-1" as PollId));

      expect(mockDeletePollVote).toHaveBeenCalledWith(asServed("poll-1" as PollId));
    });
  });

  // ============================================================================
  // CREATE POLL
  // ============================================================================

  describe("createPoll", () => {
    it("calls create API with poll data", async () => {
      const newPoll = createMockPoll("new-poll", "New Poll");
      mockPostPoll.mockResolvedValue({ data: newPoll, error: null });

      const store = usePollsStore();
      const result = await store.createPoll({
        title: "New Poll",
        startsUtc: "2025-01-01",
        endsUtc: "2025-12-31",
        options: [{ text: "A" }, { text: "B" }],
      } as any);

      expect(mockPostPoll).toHaveBeenCalled();
      expect(result.data).toEqual(newPoll);
    });

    it("adds created poll to polls list", async () => {
      const existingPoll = createMockPoll("existing", "Existing");
      mockGetPolls.mockResolvedValue({
        data: { resources: [existingPoll], paging: { current: 1, pages: 1, total: 1 } },
        error: null,
      });

      const store = usePollsStore();
      await store.fetchPolls({ number: 1 });

      const newPoll = createMockPoll("new-poll", "New Poll");
      mockPostPoll.mockResolvedValue({ data: newPoll, error: null });

      await store.createPoll({ title: "New Poll" } as any);

      expect(store.polls?.resources[0]).toEqual(newPoll);
    });

    it("returns error on failure", async () => {
      mockPostPoll.mockResolvedValue({
        data: null,
        error: { status: 400, title: "Validation error" },
      });

      const store = usePollsStore();
      const result = await store.createPoll({ title: "" } as any);

      expect(result.error).toBeTruthy();
    });
  });

  // ============================================================================
  // EDIT POLL
  // ============================================================================

  describe("editPoll", () => {
    it("calls patch API with poll data", async () => {
      const updatedPoll = createMockPoll("poll-1", "Updated Title");
      mockPatchPoll.mockResolvedValue({ data: updatedPoll, error: null });

      const store = usePollsStore();
      await store.editPoll(asServed("poll-1" as PollId), { title: "Updated Title" });

      expect(mockPatchPoll).toHaveBeenCalledWith(asServed("poll-1" as PollId), { title: "Updated Title" });
    });

    it("updates poll in store after edit", async () => {
      const poll = createMockPoll("poll-1", "Original");
      mockGetActivePolls.mockResolvedValue({
        data: { resources: [poll], paging: null },
        error: null,
      });

      const store = usePollsStore();
      await store.fetchActivePolls();

      const updated = { ...poll, title: "Updated" };
      mockPatchPoll.mockResolvedValue({ data: updated, error: null });

      await store.editPoll(asServed("poll-1" as PollId), { title: "Updated" });

      expect(store.activePolls?.[0].title).toBe("Updated");
    });

    it("returns error on failure", async () => {
      mockPatchPoll.mockResolvedValue({
        data: null,
        error: { status: 403, title: "Forbidden" },
      });

      const store = usePollsStore();
      const result = await store.editPoll(asServed("poll-1" as PollId), { title: "New" });

      expect(result.error).toBeTruthy();
    });
  });

  // ============================================================================
  // LOADING STATES
  // ============================================================================

  describe("Loading States", () => {
    it("tracks loading state for active polls", async () => {
      let resolvePromise: (value: any) => void;
      const promise = new Promise((resolve) => {
        resolvePromise = resolve;
      });
      mockGetActivePolls.mockReturnValue(promise);

      const store = usePollsStore();
      const fetchPromise = store.fetchActivePolls();

      expect(store.activePollsLoading).toBe(true);

      resolvePromise!({ data: { resources: [] }, error: null });
      await fetchPromise;

      expect(store.activePollsLoading).toBe(false);
    });
  });

  // ============================================================================
  // ERROR STATES
  // ============================================================================

  describe("Error States", () => {
    it("clears error on successful fetch", async () => {
      // First, cause an error
      mockGetActivePolls.mockResolvedValueOnce({
        data: null,
        error: { status: 500, title: "Error" },
      });

      const store = usePollsStore();
      await store.fetchActivePolls();
      expect(store.activePollsError).toBeTruthy();

      // Then, successful fetch
      mockGetActivePolls.mockResolvedValueOnce({
        data: { resources: [] },
        error: null,
      });
      await store.fetchActivePolls();

      expect(store.activePollsError).toBeNull();
    });
  });
});
