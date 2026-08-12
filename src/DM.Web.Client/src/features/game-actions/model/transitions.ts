import {
  ClosedReason,
  GameStatus,
  GameStatusTransition,
} from "@/entities/game";

export interface StatusTransitionOption {
  value: GameStatusTransition;
  label: string;
  /** Destructive / significant transition — worth a confirmation prompt. */
  danger?: boolean;
}

/**
 * Status transitions offered for the current game status. The backend rejects
 * any illegal move; this list only avoids showing obviously-inapplicable ones.
 * Single source of truth shared by the sidebar panel and the settings page.
 *
 * Mirrors the domain state machine (ModuleStatusTransition.cs):
 *   Start:  Draft -> Active
 *   Finish: Active -> Closed (Finished)
 *   Freeze: Active -> Closed (Frozen)
 *   Close:  Active or Closed+Frozen -> Closed (None)
 *   Reopen: Closed (any reason) -> Active
 */
export function availableStatusTransitions(
  status: GameStatus | undefined,
  closedReason?: ClosedReason,
): StatusTransitionOption[] {
  if (status === GameStatus.Draft) {
    return [{ value: GameStatusTransition.Start, label: "Начать игру" }];
  }
  if (status === GameStatus.Active) {
    return [
      { value: GameStatusTransition.Finish, label: "Завершить игру" },
      { value: GameStatusTransition.Freeze, label: "Заморозить игру" },
      {
        value: GameStatusTransition.Close,
        label: "Закрыть игру",
        danger: true,
      },
    ];
  }
  if (status === GameStatus.Closed) {
    // Two distinct labels for the same Reopen transition (doc 4.2.1.3):
    // a frozen game is "resumed" (Frozen->Active), a finished/closed game is
    // "re-opened" (Finished/None->Active).
    const reopenLabel =
      closedReason === ClosedReason.Frozen
        ? "Возобновить игру"
        : "Переоткрыть игру";
    const options: StatusTransitionOption[] = [
      { value: GameStatusTransition.Reopen, label: reopenLabel },
    ];
    // A frozen game can also be closed for good (Closed+Frozen -> Closed+None).
    if (closedReason === ClosedReason.Frozen) {
      options.push({
        value: GameStatusTransition.Close,
        label: "Закрыть игру",
        danger: true,
      });
    }
    return options;
  }
  return [];
}
