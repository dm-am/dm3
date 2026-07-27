import { BlogStatusTransition, type BlogStatus } from "@/entities/blog";

export interface BlogStatusTransitionOption {
  value: BlogStatusTransition;
  label: string;
  /** Destructive / significant transition — worth a confirmation prompt. */
  danger?: boolean;
}

/**
 * Status transitions offered for the current blog status. Mirrors the game
 * feature (features/game-actions/model/transitions.ts); blogs have a simpler
 * machine because ModuleStatus for blogs is Draft/Active/Closed without a
 * closed reason:
 *   Start:  Draft -> Active  ("Опубликовать блог")
 *   Close:  Active -> Closed ("Закрыть блог")
 *   Reopen: Closed -> Active ("Переоткрыть блог")
 *
 * Single source of truth shared by the sidebar panel and the settings page.
 * The backend rejects any illegal move; this list only avoids showing
 * obviously-inapplicable ones.
 */
export function availableStatusTransitions(
  status: BlogStatus | undefined,
): BlogStatusTransitionOption[] {
  if (status === "Draft") {
    return [{ value: BlogStatusTransition.Start, label: "Опубликовать блог" }];
  }
  if (status === "Active") {
    return [
      {
        value: BlogStatusTransition.Close,
        label: "Закрыть блог",
        danger: true,
      },
    ];
  }
  if (status === "Closed") {
    return [{ value: BlogStatusTransition.Reopen, label: "Переоткрыть блог" }];
  }
  return [];
}
