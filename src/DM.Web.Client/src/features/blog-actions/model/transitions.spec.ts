import { describe, it, expect } from "vitest";
import {
  BlogStatus,
  BlogStatusTransition,
  type BlogClosedReason,
} from "@/entities/blog";
import { availableStatusTransitions } from "./transitions";

/**
 * The blog half of the one status machine both modules run on
 * (src/DM.Domain.Core/Statuses/ModuleStatusPolicy.cs, reached through
 * BlogService.ChangeStatusAsync):
 *   Start:  Draft -> Active
 *   Freeze: Active -> Closed (Frozen)
 *   Close:  Active or Closed+Frozen -> Closed (None)
 *   Reopen: Closed (any reason) -> Active
 *
 * Finish is deliberately absent from the client list: the server would take it,
 * the blog UI never asks for it. Every other option here must be one the server
 * accepts from that state — a button the machine answers 400 to is a promise
 * the panel had no right to make.
 */
describe("availableStatusTransitions (blog)", () => {
  const values = (
    status: BlogStatus | undefined,
    closedReason?: BlogClosedReason,
  ) => availableStatusTransitions(status, closedReason).map((o) => o.value);

  it("offers only Start for a draft blog", () => {
    expect(values(BlogStatus.Draft)).toEqual([BlogStatusTransition.Start]);
  });

  it("offers Freeze and Close for an active blog", () => {
    expect(values(BlogStatus.Active)).toEqual([
      BlogStatusTransition.Freeze,
      BlogStatusTransition.Close,
    ]);
  });

  it("offers Reopen and Close for a frozen blog (Closed+Frozen)", () => {
    expect(values(BlogStatus.Closed, "Frozen")).toEqual([
      BlogStatusTransition.Reopen,
      BlogStatusTransition.Close,
    ]);
  });

  it("offers only Reopen for a blog closed for good (Closed+None)", () => {
    expect(values(BlogStatus.Closed, "None")).toEqual([
      BlogStatusTransition.Reopen,
    ]);
  });

  it("offers only Reopen when a closed blog carries no reason", () => {
    expect(values(BlogStatus.Closed)).toEqual([BlogStatusTransition.Reopen]);
  });

  it("never offers Freeze from a closed blog (the machine refuses it)", () => {
    const reasons: Array<BlogClosedReason | undefined> = [
      "Frozen",
      "Finished",
      "None",
      undefined,
    ];
    for (const reason of reasons) {
      expect(values(BlogStatus.Closed, reason)).not.toContain(
        BlogStatusTransition.Freeze,
      );
    }
  });

  it("offers nothing when the status is unknown", () => {
    expect(values(undefined)).toEqual([]);
  });

  it("labels Reopen 'Возобновить блог' for a frozen blog", () => {
    const reopen = availableStatusTransitions(BlogStatus.Closed, "Frozen").find(
      (o) => o.value === BlogStatusTransition.Reopen,
    );
    expect(reopen?.label).toBe("Возобновить блог");
  });

  it("labels Reopen 'Переоткрыть блог' for a blog closed with no reason", () => {
    const reopen = availableStatusTransitions(BlogStatus.Closed).find(
      (o) => o.value === BlogStatusTransition.Reopen,
    );
    expect(reopen?.label).toBe("Переоткрыть блог");
  });

  it("marks every Close option as danger (confirmation prompt)", () => {
    const states: Array<[BlogStatus, BlogClosedReason | undefined]> = [
      [BlogStatus.Active, undefined],
      [BlogStatus.Closed, "Frozen"],
    ];
    for (const [status, reason] of states) {
      const close = availableStatusTransitions(status, reason).find(
        (o) => o.value === BlogStatusTransition.Close,
      );
      expect(close?.danger).toBe(true);
    }
  });

  it("leaves Freeze undangerous — it is a pause, not an ending", () => {
    const freeze = availableStatusTransitions(BlogStatus.Active).find(
      (o) => o.value === BlogStatusTransition.Freeze,
    );
    expect(freeze?.danger).toBeUndefined();
  });
});
