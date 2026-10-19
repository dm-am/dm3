/**
 * @vitest-environment jsdom
 */

// The last activity on a profile left open: "online" for five minutes after
// it, then the time of it, with no reload in between.
import { describe, it, expect, vi, afterEach } from "vitest";
import { mount, RouterLinkStub, type VueWrapper } from "@vue/test-utils";
import { StatLine } from "@/shared/ui/StatLine";
import ProfileStats from "./ProfileStats.vue";

const NOW = new Date("2026-10-28T12:00:00Z");
const MINUTE = 60_000;

let mounted: VueWrapper | null = null;

afterEach(() => {
  mounted?.unmount();
  mounted = null;
  vi.useRealTimers();
});

function render(minutesAgo: number) {
  mounted = mount(ProfileStats, {
    props: {
      user: {
        username: "Other",
        registeredUtc: "2020-01-01T00:00:00Z",
        lastActivityUtc: new Date(
          NOW.getTime() - minutesAgo * MINUTE,
        ).toISOString(),
      } as never,
      username: "Other",
      isRemovedAccount: false,
    },
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  return mounted;
}

const lastActivity = (wrapper: VueWrapper) =>
  wrapper
    .findAllComponents(StatLine)
    .find((line) => line.props("label") === "Последняя активность")
    ?.props("value");

describe("ProfileStats last activity", () => {
  it("turns from online to the time of the activity on its own, five minutes after it", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
    const wrapper = render(4);
    expect(lastActivity(wrapper)).toBe("online");

    // Five minutes after the activity, the mark still holds.
    await vi.advanceTimersByTimeAsync(MINUTE);
    expect(lastActivity(wrapper)).toBe("online");

    await vi.advanceTimersByTimeAsync(MINUTE);
    expect(lastActivity(wrapper)).not.toBe("online");
    expect(lastActivity(wrapper)).toMatch(/\d{2}:\d{2}/);
  });
});

// The opt-in of the rating is in the personal info of the edit mode, not among the counters.
describe("ProfileStats of the owner", () => {
  it("offers no opt-in of the rating among the counters", () => {
    const wrapper = render(30);

    expect(wrapper.find('input[type="checkbox"]').exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Участие в рейтинге");
  });
});
