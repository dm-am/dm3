/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, config } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { nextTick } from "vue";
import Poll from "./Poll.vue";
import type { Poll as PollType, PollOption, PollId, PollOptionId } from "@/entities/poll";
import { PollStatus } from "@/entities/poll";
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

// Stub router-link
config.global.stubs = {
  "router-link": {
    template: '<a><slot /></a>',
    props: ["to"],
  },
};

// Mock dayjs
vi.mock("dayjs", () => {
  const dayjs = (date?: string | Date) => ({
    isBefore: vi.fn(() => false),
    format: vi.fn(() => "01.01.2025 12:00"),
    add: vi.fn(() => dayjs()),
  });
  dayjs.extend = vi.fn();
  return { default: dayjs };
});

// Mock stores
vi.mock("@/entities/user", () => ({
  useUserStore: () => ({
    user: null,
  }),
  userIsSeniorModerator: vi.fn(() => false),
}));

vi.mock("@/entities/poll", () => ({
  usePollsStore: () => ({
    vote: vi.fn(),
    unvote: vi.fn(),
    editPoll: vi.fn().mockResolvedValue({ data: null, error: null }),
  }),
  PollStatus: {
    Pending: "Pending",
    Active: "Active",
    Closed: "Closed",
  },
}));

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

describe("Poll", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders without errors", () => {
      const wrapper = mount(Poll, {
        props: { poll: createMockPoll() },
      });
      expect(wrapper.exists()).toBe(true);
    });

    it("displays poll title", () => {
      const wrapper = mount(Poll, {
        props: { poll: createMockPoll({ title: "My Poll Title" }) },
      });
      expect(wrapper.find(".poll-title").text()).toContain("My Poll Title");
    });

    it("displays all poll options", () => {
      const poll = createMockPoll({
        options: [
          createPollOption("1", "First", 1, false),
          createPollOption("2", "Second", 2, false),
          createPollOption("3", "Third", 3, false),
        ],
      });
      const wrapper = mount(Poll, {
        props: { poll },
      });

      const progressBars = wrapper.findAllComponents({ name: "ProgressBar" });
      expect(progressBars.length).toBe(3);
    });

    it("has poll class on container", () => {
      const wrapper = mount(Poll, {
        props: { poll: createMockPoll() },
      });
      expect(wrapper.find(".poll").exists()).toBe(true);
    });
  });

  // ============================================================================
  // VOTES COUNT
  // ============================================================================

  describe("Votes Count", () => {
    it("displays vote count for each option", () => {
      const poll = createMockPoll({
        options: [
          createPollOption("1", "Option A", 7, false),
          createPollOption("2", "Option B", 3, false),
        ],
      });
      const wrapper = mount(Poll, {
        props: { poll },
      });

      expect(wrapper.text()).toContain("7");
      expect(wrapper.text()).toContain("3");
    });
  });

  // ============================================================================
  // CONTROLS MODE
  // ============================================================================

  describe("Controls Mode", () => {
    it("does not show edit link when controls is false", () => {
      const wrapper = mount(Poll, {
        props: { poll: createMockPoll(), controls: false },
      });
      expect(wrapper.find(".poll-edit-link").exists()).toBe(false);
    });
  });

  // ============================================================================
  // VOTED STATE
  // ============================================================================

  describe("Voted State", () => {
    it("highlights voted option", () => {
      const poll = createMockPoll({
        options: [
          createPollOption("1", "A", 5, true),
          createPollOption("2", "B", 3, false),
        ],
      });
      const wrapper = mount(Poll, {
        props: { poll },
      });

      expect(wrapper.find(".poll-option-voted").exists()).toBe(true);
    });

    it("shows tick icon for voted option", () => {
      const poll = createMockPoll({
        options: [
          createPollOption("1", "A", 5, true),
          createPollOption("2", "B", 3, false),
        ],
      });
      const wrapper = mount(Poll, {
        props: { poll },
      });

      // Icon component should be rendered for voted option
      const icons = wrapper.findAllComponents({ name: "Icon" });
      expect(icons.length).toBeGreaterThan(0);
    });
  });

  // ============================================================================
  // STATUS
  // ============================================================================

  describe("Status Display", () => {
    it("shows status inline", () => {
      const wrapper = mount(Poll, {
        props: { poll: createMockPoll() },
      });
      expect(wrapper.find(".poll-status-inline").exists()).toBe(true);
    });

    it("displays status text for active poll", () => {
      const wrapper = mount(Poll, {
        props: { poll: createMockPoll({ status: asServed(PollStatus.Active) }) },
      });
      expect(wrapper.text()).toContain("Активен до");
    });
  });

  // ============================================================================
  // EDIT MODE (for moderators)
  // ============================================================================

  describe("Edit Mode", () => {
    it("does not show edit link by default", () => {
      const wrapper = mount(Poll, {
        props: { poll: createMockPoll(), detailed: true },
      });
      // Edit link should not be visible for non-moderators
      expect(wrapper.find(".poll-edit-link").exists()).toBe(false);
    });
  });

  // ============================================================================
  // PROGRESS BARS
  // ============================================================================

  describe("Progress Bars", () => {
    it("renders ProgressBar for each option", () => {
      const poll = createMockPoll({
        options: [
          createPollOption("1", "A", 5, false),
          createPollOption("2", "B", 3, false),
        ],
      });
      const wrapper = mount(Poll, {
        props: { poll },
      });

      const progressBars = wrapper.findAllComponents({ name: "ProgressBar" });
      expect(progressBars.length).toBe(2);
    });

    it("passes correct current value to ProgressBar", () => {
      const poll = createMockPoll({
        options: [
          createPollOption("1", "A", 7, false),
        ],
      });
      const wrapper = mount(Poll, {
        props: { poll },
      });

      const progressBar = wrapper.findComponent({ name: "ProgressBar" });
      expect(progressBar.props("current")).toBe(7);
    });
  });

  // ============================================================================
  // VOTING UI (for anonymous users, no vote buttons)
  // ============================================================================

  describe("Voting UI", () => {
    it("does not show vote buttons for anonymous users", () => {
      const wrapper = mount(Poll, {
        props: { poll: createMockPoll() },
      });
      // The poll-option-vote class is the clickable overlay for voting
      expect(wrapper.find(".poll-option-vote").exists()).toBe(false);
    });
  });
});
