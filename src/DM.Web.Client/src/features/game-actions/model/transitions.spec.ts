import { describe, it, expect } from "vitest";
import {
  ClosedReason,
  GameStatus,
  GameStatusTransition,
} from "@/entities/game";
import { availableStatusTransitions } from "./transitions";

/**
 * Per-status option matrix must mirror the domain state machine
 * (src/DM.Domain.Core/Statuses/ModuleStatusTransition.cs):
 *   Start:  Draft -> Active
 *   Finish: Active -> Closed (Finished)
 *   Freeze: Active -> Closed (Frozen)
 *   Close:  Active or Closed+Frozen -> Closed (None)
 *   Reopen: Closed (any reason) -> Active
 */
describe("availableStatusTransitions", () => {
  const values = (
    status: GameStatus | undefined,
    closedReason?: ClosedReason,
  ) => availableStatusTransitions(status, closedReason).map((o) => o.value);

  it("offers only Start for a draft game", () => {
    expect(values(GameStatus.Draft)).toEqual([GameStatusTransition.Start]);
  });

  it("offers Finish, Freeze and Close for an active game", () => {
    expect(values(GameStatus.Active)).toEqual([
      GameStatusTransition.Finish,
      GameStatusTransition.Freeze,
      GameStatusTransition.Close,
    ]);
  });

  it("offers Reopen and Close for a frozen game (Closed+Frozen)", () => {
    expect(values(GameStatus.Closed, ClosedReason.Frozen)).toEqual([
      GameStatusTransition.Reopen,
      GameStatusTransition.Close,
    ]);
  });

  it("offers only Reopen for a finished game (Closed+Finished)", () => {
    expect(values(GameStatus.Closed, ClosedReason.Finished)).toEqual([
      GameStatusTransition.Reopen,
    ]);
  });

  it("offers only Reopen for a closed game (Closed+None)", () => {
    expect(values(GameStatus.Closed, ClosedReason.None)).toEqual([
      GameStatusTransition.Reopen,
    ]);
  });

  it("offers only Reopen when a closed game carries no reason", () => {
    expect(values(GameStatus.Closed)).toEqual([GameStatusTransition.Reopen]);
  });

  it("offers nothing when the status is unknown", () => {
    expect(values(undefined)).toEqual([]);
  });

  it("labels Reopen 'Возобновить игру' for a frozen game", () => {
    const options = availableStatusTransitions(
      GameStatus.Closed,
      ClosedReason.Frozen,
    );
    const reopen = options.find((o) => o.value === GameStatusTransition.Reopen);
    expect(reopen?.label).toBe("Возобновить игру");
  });

  it("labels Reopen 'Переоткрыть игру' for a finished game", () => {
    const options = availableStatusTransitions(
      GameStatus.Closed,
      ClosedReason.Finished,
    );
    const reopen = options.find((o) => o.value === GameStatusTransition.Reopen);
    expect(reopen?.label).toBe("Переоткрыть игру");
  });

  it("labels Reopen 'Переоткрыть игру' for a closed game with no reason", () => {
    const reopen = availableStatusTransitions(GameStatus.Closed).find(
      (o) => o.value === GameStatusTransition.Reopen,
    );
    expect(reopen?.label).toBe("Переоткрыть игру");
  });

  it("marks every Close option as danger (confirmation prompt)", () => {
    const statuses: Array<[GameStatus, ClosedReason | undefined]> = [
      [GameStatus.Active, undefined],
      [GameStatus.Closed, ClosedReason.Frozen],
    ];
    for (const [status, reason] of statuses) {
      const close = availableStatusTransitions(status, reason).find(
        (o) => o.value === GameStatusTransition.Close,
      );
      expect(close?.danger).toBe(true);
    }
  });
});
