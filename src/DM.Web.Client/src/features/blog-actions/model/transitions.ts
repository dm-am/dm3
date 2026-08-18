import { ModuleStatus } from "@/shared/api/models/common";
import {
  BlogStatusTransition,
  type BlogClosedReason,
  type BlogStatus,
} from "@/entities/blog";

export interface BlogStatusTransitionOption {
  value: BlogStatusTransition;
  label: string;
  /** Destructive / significant transition — worth a confirmation prompt. */
  danger?: boolean;
}

/**
 * Status transitions offered for the current blog status. Mirrors the game
 * feature (features/game-actions/model/transitions.ts), because both modules
 * run on one state machine (ModuleStatusPolicy.cs):
 *   Start:  Draft -> Active                  ("Опубликовать блог")
 *   Freeze: Active -> Closed (Frozen)        ("Заморозить блог")
 *   Close:  Active or Closed+Frozen -> Closed (None)  ("Закрыть блог")
 *   Reopen: Closed (any reason) -> Active    ("Возобновить"/"Переоткрыть блог")
 *
 * Finish is the one game move a blog does not offer: a blog is not a story
 * that ends. The backend would accept it; nothing on the client asks for it.
 *
 * Single source of truth shared by the sidebar panel and the settings page.
 * The backend rejects any illegal move; this list only avoids showing
 * obviously-inapplicable ones.
 */
export function availableStatusTransitions(
  status: BlogStatus | undefined,
  closedReason?: BlogClosedReason,
): BlogStatusTransitionOption[] {
  if (status === ModuleStatus.Draft) {
    return [{ value: BlogStatusTransition.Start, label: "Опубликовать блог" }];
  }
  if (status === ModuleStatus.Active) {
    return [
      { value: BlogStatusTransition.Freeze, label: "Заморозить блог" },
      {
        value: BlogStatusTransition.Close,
        label: "Закрыть блог",
        danger: true,
      },
    ];
  }
  if (status === ModuleStatus.Closed) {
    // Two labels for the same Reopen move, as on a game: a frozen blog is
    // "resumed", a blog closed for good is "re-opened".
    const options: BlogStatusTransitionOption[] = [
      {
        value: BlogStatusTransition.Reopen,
        label:
          closedReason === "Frozen" ? "Возобновить блог" : "Переоткрыть блог",
      },
    ];
    // Closed+Frozen -> Closed+None: a paused blog can be given up on without
    // being reopened first. The machine allows Close from no other closed state.
    if (closedReason === "Frozen") {
      options.push({
        value: BlogStatusTransition.Close,
        label: "Закрыть блог",
        danger: true,
      });
    }
    return options;
  }
  return [];
}
