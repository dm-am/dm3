import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import type { SubscriberRef, Username } from "@/shared/api/models/common";
import { SubscriptionSettings } from "@/shared/api/models/subscriptions";
import ProfileSubscribersSection from "./ProfileSubscribersSection.vue";

const GAMES = SubscriptionSettings.AuthorGameEvents;
const BLOGS = SubscriptionSettings.AuthorBlogEvents;

function subscriber(
  username: string,
  settings: number,
  lastActivityUtc: string | null = "2026-07-29T00:00:00Z",
): SubscriberRef {
  // Username is branded; a literal in a test is exactly the case the brand
  // exists to keep out of production code, so the cast stays here.
  return {
    username: username as unknown as Username,
    settings,
    lastActivityUtc,
  };
}

function render(subscribers: SubscriberRef[], total: number) {
  return mount(ProfileSubscribersSection, {
    props: { subscribers, label: "Подписаны на игры", flag: GAMES, total },
    global: {
      stubs: {
        // The link's text is what the assertions read, so the stub has to keep
        // the slot rather than swallow it.
        RouterLink: { template: "<a><slot /></a>" },
      },
    },
  });
}

describe("ProfileSubscribersSection", () => {
  it("lists the subscribers of its own category and no others", () => {
    const wrapper = render(
      [subscriber("gamer", GAMES), subscriber("blogger", BLOGS)],
      1,
    );

    expect(wrapper.text()).toContain("gamer");
    expect(wrapper.text()).not.toContain("blogger");
  });

  it("says nothing when the category has no subscribers", () => {
    const wrapper = render([subscriber("blogger", BLOGS)], 0);

    // Not an empty line and not an empty-state string: the host page composes
    // several sections and a blank one is noise.
    expect(wrapper.find(".subscribers-line").exists()).toBe(false);
  });

  it("shows the count alone when the preview carries no name of this category", () => {
    // The whole defect this guards: the server caps the preview at 20 by
    // activity before the categories are considered, so a populated category
    // can contribute nothing to it. The line used to vanish.
    const wrapper = render([subscriber("blogger", BLOGS)], 431);

    expect(wrapper.find(".subscribers-line").exists()).toBe(true);
    expect(wrapper.text()).toContain("431");
    expect(wrapper.text()).not.toContain("blogger");
  });

  it("counts the ones it could not name", () => {
    const wrapper = render(
      [subscriber("gamer", GAMES), subscriber("other", GAMES)],
      5,
    );

    expect(wrapper.text()).toContain("gamer");
    expect(wrapper.text()).toContain("other");
    expect(wrapper.text()).toContain("и еще 3");
  });

  it("adds no tail when the names cover the count", () => {
    const wrapper = render([subscriber("gamer", GAMES)], 1);

    expect(wrapper.text()).toContain("gamer");
    expect(wrapper.text()).not.toContain("и еще");
  });

  it("adds no tail when the preview somehow holds more than the count", () => {
    // Should not happen — but a negative tail would be worse than none.
    const wrapper = render(
      [subscriber("gamer", GAMES), subscriber("other", GAMES)],
      1,
    );

    expect(wrapper.text()).not.toContain("и еще");
  });

  it("puts the recently active first and the never-active last", () => {
    const wrapper = render(
      [
        subscriber("never", GAMES, null),
        subscriber("older", GAMES, "2026-01-01T00:00:00Z"),
        subscriber("fresh", GAMES, new Date().toISOString()),
      ],
      3,
    );

    const names = wrapper.findAll("a").map((link) => link.text());
    expect(names).toEqual(["fresh", "older", "never"]);
  });
});
