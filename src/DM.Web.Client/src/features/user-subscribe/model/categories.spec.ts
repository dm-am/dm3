/**
 * The words a subscription to a person is said in, and the number it is sent
 * as. The list of subscriptions says a person's row in words ("игры, топики"),
 * and the panel sends the kinds back with the channels the subscription
 * already holds: the server replaces the whole number, so a channel left out
 * is a channel switched off.
 */
import { describe, it, expect } from "vitest";
import { SubscriptionSettings } from "@/shared/api/models/subscriptions";
import { describeUserSubscription, withCategories } from "./categories";

const {
  AuthorGameEvents: GAMES,
  AuthorBlogEvents: BLOGS,
  AuthorTopicEvents: TOPICS,
  InApp,
  Email,
} = SubscriptionSettings;

describe("describeUserSubscription", () => {
  it("names the kinds held, in the order of the panel", () => {
    expect(describeUserSubscription(InApp | TOPICS | GAMES)).toBe(
      "игры, топики",
    );
    expect(
      describeUserSubscription(SubscriptionSettings.UserSubscriptionDefault),
    ).toBe("игры, блоги, топики");
  });

  it("says nothing of the channels", () => {
    expect(describeUserSubscription(InApp | Email)).toBe("");
  });
});

describe("withCategories", () => {
  it("keeps the channels the subscription holds", () => {
    expect(withCategories(InApp | Email | GAMES, BLOGS)).toBe(
      InApp | Email | BLOGS,
    );
  });

  it("gives a subscription with no channel the one the site delivers through", () => {
    expect(withCategories(GAMES, GAMES | TOPICS)).toBe(InApp | GAMES | TOPICS);
  });
});
