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
  // Username is branded, so a literal is cast here.
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
        // The text of the link is read, so the stub keeps the slot.
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

    // No empty line and no empty-state string: the host page composes several sections.
    expect(wrapper.find(".subscribers-line").exists()).toBe(false);
  });

  it("shows the count alone when the preview carries no name of this category", () => {
    // The server caps the preview before the categories are counted, so a full category can bring no name.
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

  it("names them in the order the server sent", () => {
    const wrapper = render(
      [
        subscriber("never", GAMES, null),
        subscriber("older", GAMES, "2026-01-01T00:00:00Z"),
        subscriber("fresh", GAMES, new Date().toISOString()),
      ],
      3,
    );

    const names = wrapper.findAll("a").map((link) => link.text());
    expect(names).toEqual(["never", "older", "fresh"]);
  });
});
