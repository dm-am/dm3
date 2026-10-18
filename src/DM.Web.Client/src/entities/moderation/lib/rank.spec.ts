/**
 * Whom a moderation action reaches, as the screens read it: the server's
 * ModerationRank.Reaches, strictly below the reader's rank and never an
 * administrator. The owner (05.10) on whether a moderator may warn a senior
 * moderator or an administrator: "нет".
 */
import { describe, it, expect } from "vitest";
import { UserRole } from "@/shared/api/models/common";
import { reachesByRank } from "./rank";

const as = (role: UserRole) => ({ role });

describe("the rank a moderation action reaches", () => {
  it.each([
    [UserRole.Moderator, UserRole.RegularUser],
    [UserRole.Moderator, UserRole.Mentor],
    [UserRole.Moderator, UserRole.Guest],
    [UserRole.SeniorModerator, UserRole.Moderator],
    [UserRole.Admin, UserRole.SeniorModerator],
  ])("%s reaches %s", (actor, target) => {
    expect(reachesByRank(as(actor), as(target))).toBe(true);
  });

  it.each([
    [UserRole.Moderator, UserRole.Moderator],
    [UserRole.Moderator, UserRole.SeniorModerator],
    [UserRole.Moderator, UserRole.Admin],
    [UserRole.SeniorModerator, UserRole.SeniorModerator],
    [UserRole.SeniorModerator, UserRole.Admin],
    [UserRole.Admin, UserRole.Admin],
    [UserRole.Admin, UserRole.System],
  ])("%s does not reach %s", (actor, target) => {
    expect(reachesByRank(as(actor), as(target))).toBe(false);
  });

  // Below a moderator nobody warns anybody, whatever the rank of the other.
  it.each([UserRole.Mentor, UserRole.RegularUser, UserRole.Guest])(
    "%s reaches nobody",
    (actor) => {
      expect(reachesByRank(as(actor), as(UserRole.Guest))).toBe(false);
    },
  );

  // A reader who is not signed in, and an author whose account is gone.
  it("reaches nobody without a reader or without a person to reach", () => {
    expect(reachesByRank(null, as(UserRole.RegularUser))).toBe(false);
    expect(reachesByRank(as(UserRole.Admin), null)).toBe(false);
  });
});
