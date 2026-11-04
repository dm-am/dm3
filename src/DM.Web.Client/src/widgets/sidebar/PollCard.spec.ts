/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import Poll from "./PollCard.vue";
import type {
  Poll as PollType,
  PollOption,
  PollId,
  PollOptionId,
} from "@/entities/poll";
import { PollStatus } from "@/entities/poll";
import { useAuthStore } from "@/entities/user";
import { asServed } from "@/shared/api/models";

const api = vi.hoisted(() => ({
  postPollVote: vi.fn(),
  deletePollVote: vi.fn(),
}));
vi.mock("@/entities/poll/api/pollApi", () => ({ default: api }));

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

const createMockPoll = (overrides: Partial<PollType> = {}): PollType =>
  ({
    id: asServed("poll-1" as PollId),
    title: "Test Poll",
    details: null,
    startsUtc: "2025-01-01T00:00:00Z",
    endsUtc: "2025-12-31T23:59:59Z",
    status: asServed(PollStatus.Active),
    isAnonymous: true,
    options: [
      createPollOption("opt-1", "Option 1", 5, false),
      createPollOption("opt-2", "Option 2", 3, false),
    ],
    ...overrides,
  }) as PollType;

const render = (poll: PollType) => mount(Poll, { props: { poll } });

const signIn = () => {
  const auth = useAuthStore();
  auth.user = { username: "Кто-то" } as NonNullable<typeof auth.user>;
};

describe("Poll", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    api.postPollVote.mockResolvedValue({ data: null, error: null });
    api.deletePollVote.mockResolvedValue({ data: null, error: null });
  });

  describe("Votes Count", () => {
    it("draws a bar for each option", () => {
      const poll = createMockPoll({
        options: [
          createPollOption("1", "First", 1, false),
          createPollOption("2", "Second", 2, false),
          createPollOption("3", "Third", 3, false),
        ],
      });

      const progressBars = render(poll).findAllComponents({
        name: "ProgressBar",
      });
      expect(progressBars.length).toBe(3);
    });

    it("passes correct current value to ProgressBar", () => {
      const poll = createMockPoll({
        options: [createPollOption("1", "A", 7, false)],
      });

      const progressBar = render(poll).findComponent({ name: "ProgressBar" });
      expect(progressBar.props("current")).toBe(7);
    });
  });

  describe("Voted State", () => {
    const votedPoll = () =>
      createMockPoll({
        options: [
          createPollOption("1", "A", 5, true),
          createPollOption("2", "B", 3, false),
        ],
      });

    it("highlights voted option", () => {
      expect(render(votedPoll()).find(".poll-option-voted").exists()).toBe(
        true,
      );
    });

    it("shows checkmark symbol for voted option", () => {
      expect(render(votedPoll()).text()).toContain("\u2713");
    });
  });

  describe("Voting", () => {
    it("does not show vote buttons for anonymous users", () => {
      expect(render(createMockPoll()).find(".poll-option-vote").exists()).toBe(
        false,
      );
    });

    it("votes for the option clicked", async () => {
      signIn();
      const wrapper = render(createMockPoll());

      await wrapper
        .find('[aria-label="Проголосовать за: Option 2"]')
        .trigger("click");
      await flushPromises();

      expect(api.postPollVote).toHaveBeenCalledWith("poll-1", "opt-2");
    });

    it("cancels the vote from the option voted for", async () => {
      signIn();
      const wrapper = render(
        createMockPoll({
          options: [
            createPollOption("opt-1", "Option 1", 5, true),
            createPollOption("opt-2", "Option 2", 3, false),
          ],
        }),
      );

      expect(wrapper.find('[aria-label^="Проголосовать за"]').exists()).toBe(
        false,
      );
      await wrapper.find('[aria-label="Отменить голос"]').trigger("click");
      await flushPromises();

      expect(api.deletePollVote).toHaveBeenCalledWith("poll-1");
      expect(api.postPollVote).not.toHaveBeenCalled();
    });
  });
});
