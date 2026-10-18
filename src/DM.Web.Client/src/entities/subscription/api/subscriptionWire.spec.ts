/**
 * @vitest-environment node
 */

/**
 * The server names the enums of a subscription and the client's model numbers
 * them (see subscriptionWire.ts). The values below are the ones the API of the
 * stand wrote on 27 September 2026, not ones made up to fit the reader.
 */
import { describe, expect, it } from "vitest";
import {
  SubscriptionSettings,
  SubscriptionTargetType,
} from "@/shared/api/models/subscriptions";
import {
  settingsFromWire,
  subscriptionFromWire,
  targetTypeFromWire,
} from "./subscriptionWire";

describe("a subscription off the wire", () => {
  it.each([
    ["Game", SubscriptionTargetType.Game],
    ["Blog", SubscriptionTargetType.Blog],
    ["Topic", SubscriptionTargetType.Topic],
    ["User", SubscriptionTargetType.User],
  ])("reads the type %s", (wire, type) => {
    expect(targetTypeFromWire(wire)).toBe(type);
  });

  it("keeps a type written as its number", () => {
    expect(targetTypeFromWire(3)).toBe(SubscriptionTargetType.Topic);
  });

  it("leaves a type it does not know as it came", () => {
    expect(targetTypeFromWire("Board")).toBe("Board");
  });

  it.each([
    ["None", SubscriptionSettings.None],
    ["BlogReaderDefault", SubscriptionSettings.BlogReaderDefault],
    [
      "InApp, AuthorGameEvents",
      SubscriptionSettings.InApp | SubscriptionSettings.AuthorGameEvents,
    ],
    [
      "NewPosts, StatusChanges, InApp, Email",
      SubscriptionSettings.NewPosts |
        SubscriptionSettings.StatusChanges |
        SubscriptionSettings.InApp |
        SubscriptionSettings.Email,
    ],
  ])("reads the settings %s as their bits", (wire, bits) => {
    expect(settingsFromWire(wire)).toBe(bits);
  });

  // A combination with a bit that has no name (bit 6 is vacant) is written by
  // the converter as its number.
  it("reads settings written as a number", () => {
    expect(settingsFromWire(64 | 128)).toBe(192);
    expect(settingsFromWire("192")).toBe(192);
  });

  it("reads a whole row and leaves the rest of it alone", () => {
    expect(
      subscriptionFromWire({
        id: "4cc7e98c-41a0-ac9d-4d6b-aa2bf43e4663",
        targetType: "Game",
        targetId: "c55991d1-db32-c255-2519-22089f3409d3",
        targetTitle: "Последний Рубеж",
        targetStatus: "Closed",
        targetActivatedUtc: "2026-01-10T12:00:00+00:00",
        settings: "None",
        createdUtc: "2026-09-16T00:09:26.612346+00:00",
      }),
    ).toEqual({
      id: "4cc7e98c-41a0-ac9d-4d6b-aa2bf43e4663",
      targetType: SubscriptionTargetType.Game,
      targetId: "c55991d1-db32-c255-2519-22089f3409d3",
      targetTitle: "Последний Рубеж",
      // A status is a name on both sides, and the page compares names.
      targetStatus: "Closed",
      targetActivatedUtc: "2026-01-10T12:00:00+00:00",
      settings: SubscriptionSettings.None,
      createdUtc: "2026-09-16T00:09:26.612346+00:00",
    });
  });
});
