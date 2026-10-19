/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type {
  CreatePollRequest,
  Poll,
  PollId,
  PollOption,
  PollOptionId,
} from "./types";
import { PollStatus } from "./types";

function createPollOption(
  id: string,
  text: string,
  votesCount: number,
  voted: boolean | null,
): PollOption {
  return {
    id: asServed(id as PollOptionId),
    text,
    votesCount: asServed(votesCount),
    voted: asServed(voted),
    voters: null,
    totalVoters: null,
  };
}

const {
  mockGetPolls,
  mockGetActivePolls,
  mockPostPollVote,
  mockDeletePollVote,
  mockPostPoll,
  mockPatchPoll,
} = vi.hoisted(() => ({
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
import { asServed } from "@/shared/api/models";

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

const pollInput = (title: string): CreatePollRequest => ({
  title,
  details: null,
  startsUtc: "2025-01-01T00:00:00Z",
  endsUtc: "2025-12-31T23:59:59Z",
  isAnonymous: true,
  options: [],
});

describe("usePollsStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("fetchActivePolls", () => {
    it("fetches active polls", async () => {
      const mockPolls = [
        createMockPoll("1", "Poll 1"),
        createMockPoll("2", "Poll 2"),
      ];
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

  describe("fetchPolls", () => {
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

  describe("vote", () => {
    it("updates poll in active polls after voting", async () => {
      const poll = createMockPoll("poll-1", "Poll");
      mockGetActivePolls.mockResolvedValue({
        data: { resources: [poll], paging: null },
        error: null,
      });

      const store = usePollsStore();
      await store.fetchActivePolls();

      const updatedPoll = {
        ...poll,
        options: [
          {
            ...poll.options[0],
            voted: asServed<boolean | null>(true),
            votesCount: asServed(6),
          },
          poll.options[1],
        ],
      };
      mockPostPollVote.mockResolvedValue({ data: updatedPoll, error: null });

      await store.vote(
        asServed("poll-1" as PollId),
        asServed("opt-1" as PollOptionId),
      );

      expect(store.activePolls?.[0].options[0].voted).toBe(
        asServed<boolean | null>(true),
      );
    });
  });

  describe("createPoll", () => {
    it("adds created poll to polls list", async () => {
      const existingPoll = createMockPoll("existing", "Existing");
      mockGetPolls.mockResolvedValue({
        data: {
          resources: [existingPoll],
          paging: { current: 1, pages: 1, total: 1 },
        },
        error: null,
      });

      const store = usePollsStore();
      await store.fetchPolls({ number: 1 });

      const newPoll = createMockPoll("new-poll", "New Poll");
      mockPostPoll.mockResolvedValue({ data: newPoll, error: null });

      await store.createPoll(pollInput("New Poll"));

      expect(store.polls?.resources[0]).toEqual(newPoll);
    });

    it("returns error on failure", async () => {
      mockPostPoll.mockResolvedValue({
        data: null,
        error: { status: 400, title: "Validation error" },
      });

      const store = usePollsStore();
      const result = await store.createPoll(pollInput(""));

      expect(result.error).toBeTruthy();
    });
  });

  describe("editPoll", () => {
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
      const result = await store.editPoll(asServed("poll-1" as PollId), {
        title: "New",
      });

      expect(result.error).toBeTruthy();
    });
  });

  describe("Loading States", () => {
    it("tracks loading state for active polls", async () => {
      let resolvePromise: (value: unknown) => void;
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

  describe("Error States", () => {
    it("clears error on successful fetch", async () => {
      mockGetActivePolls.mockResolvedValueOnce({
        data: null,
        error: { status: 500, title: "Error" },
      });

      const store = usePollsStore();
      await store.fetchActivePolls();
      expect(store.activePollsError).toBeTruthy();

      mockGetActivePolls.mockResolvedValueOnce({
        data: { resources: [] },
        error: null,
      });
      await store.fetchActivePolls();

      expect(store.activePollsError).toBeNull();
    });
  });
});
