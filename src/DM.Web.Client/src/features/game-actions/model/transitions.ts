import { GameStatusTransition } from "@/entities/game";
import { BlogStatusTransition } from "@/entities/blog";
import { ClosedReason, ModuleStatus } from "@/shared/api/models/common";
import type { StatusTransitionOption } from "@/shared/ui/StatusButtons";

/** What a game and a blog say differently over the one machine of ModuleStatusTransition.cs. */
export interface StatusMachineWords<T extends string> {
  /** The module's transitions; a module without Finish is never offered it. */
  moves: { Start: T; Freeze: T; Close: T; Reopen: T; Finish?: T };
  /** The verb that takes a draft live. */
  start: string;
  /** The module in the accusative, as every label ends. */
  noun: string;
}

export const GAME_STATUS_WORDS: StatusMachineWords<GameStatusTransition> = {
  moves: GameStatusTransition,
  start: "Начать",
  noun: "игру",
};

// A blog is not a story that ends: its transitions have no Finish.
export const BLOG_STATUS_WORDS: StatusMachineWords<BlogStatusTransition> = {
  moves: BlogStatusTransition,
  start: "Опубликовать",
  noun: "блог",
};

// Hides obviously inapplicable moves; the server rejects any illegal one (ModuleStatusTransition.cs).
export function availableStatusTransitions<T extends string>(
  { moves, start, noun }: StatusMachineWords<T>,
  status: ModuleStatus | undefined,
  closedReason?: ClosedReason,
): StatusTransitionOption<T>[] {
  const close = { value: moves.Close, label: `Закрыть ${noun}`, danger: true };
  if (status === ModuleStatus.Draft) {
    return [{ value: moves.Start, label: `${start} ${noun}` }];
  }
  if (status === ModuleStatus.Active) {
    const finish = moves.Finish
      ? [{ value: moves.Finish, label: `Завершить ${noun}` }]
      : [];
    return [
      ...finish,
      { value: moves.Freeze, label: `Заморозить ${noun}` },
      close,
    ];
  }
  if (status === ModuleStatus.Closed) {
    // A frozen module is resumed, or given up on without being reopened first; any other is re-opened.
    if (closedReason === ClosedReason.Frozen) {
      return [{ value: moves.Reopen, label: `Возобновить ${noun}` }, close];
    }
    return [{ value: moves.Reopen, label: `Переоткрыть ${noun}` }];
  }
  return [];
}
