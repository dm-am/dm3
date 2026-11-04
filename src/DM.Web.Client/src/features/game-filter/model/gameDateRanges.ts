import type { GamesFilterState } from "./types";

type GameDateField = Extract<keyof GamesFilterState, `${string}Utc`>;

type GameDateSetter =
  | "setCreatedRange"
  | "setActivatedRange"
  | "setRecruitmentStartedRange"
  | "setClosedRange";

interface GameDateRange {
  /** Its level of the "Даты" menu, as the menu's path names it. */
  key: string;
  /** The item of the menu and the title of its level. */
  label: string;
  /** The line under the item. */
  hint: string;
  /** The prefix of its bubble. */
  bubble: string;
  from: GameDateField;
  to: GameDateField;
  setter: GameDateSetter;
}

// In the order of the menu and of the bubbles.
export const GAME_DATE_RANGES = [
  {
    key: "created",
    label: "Создание игры",
    hint: "По дате создания игры",
    bubble: "Создание:",
    from: "createdFromUtc",
    to: "createdToUtc",
    setter: "setCreatedRange",
  },
  {
    key: "activated",
    label: "Начало игры",
    hint: "По дате начала игры",
    bubble: "Начало игры:",
    from: "activatedFromUtc",
    to: "activatedToUtc",
    setter: "setActivatedRange",
  },
  {
    key: "recruitmentstarted",
    label: "Начало последнего набора",
    hint: "По дате последнего набора",
    bubble: "Начало набора:",
    from: "recruitmentStartedFromUtc",
    to: "recruitmentStartedToUtc",
    setter: "setRecruitmentStartedRange",
  },
  {
    key: "closed",
    label: "Закрытие игры",
    hint: "По дате закрытия игры",
    bubble: "Закрытие:",
    from: "closedFromUtc",
    to: "closedToUtc",
    setter: "setClosedRange",
  },
] as const satisfies readonly GameDateRange[];

export type GameDateRangeKey = (typeof GAME_DATE_RANGES)[number]["key"];

export function gameDateRange(key: GameDateRangeKey): GameDateRange;
export function gameDateRange(
  key: string | undefined,
): GameDateRange | undefined;
export function gameDateRange(
  key: string | undefined,
): GameDateRange | undefined {
  return GAME_DATE_RANGES.find((range) => range.key === key);
}

export const rangeOf = (state: GamesFilterState, range: GameDateRange) => ({
  from: state[range.from],
  to: state[range.to],
});

export const hasRange = (state: GamesFilterState, range: GameDateRange) =>
  state[range.from] !== null || state[range.to] !== null;
