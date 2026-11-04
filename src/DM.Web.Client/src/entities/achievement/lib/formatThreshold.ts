import {
  AchievementMetric,
  ContestType,
} from "@/shared/api/models/achievements";
import { pluralize } from "@/shared/lib/utils/pluralize";

/** A metric value in its display unit: registration days are shown as years. */
export function metricDisplayNumber(
  metric: AchievementMetric,
  value: number,
): number {
  if (metric === AchievementMetric.DaysSinceRegistration) {
    // ceil rounding in the seed makes a tier fire on the anniversary day;
    // round back here recovers the human 1/5/10/15 years.
    return Math.round(value / 365.25);
  }
  return value;
}

type UnitWords = [one: string, few: string, many: string];

const GAMES: UnitWords = ["игра", "игры", "игр"];

/** The unit word of a counted metric, in its three forms. */
const UNIT_WORDS: Partial<Record<AchievementMetric, UnitWords>> = {
  [AchievementMetric.GamePostsAuthored]: ["пост", "поста", "постов"],
  [AchievementMetric.GamesHosted]: GAMES,
  [AchievementMetric.GamesPlayed]: GAMES,
  [AchievementMetric.GameDrops]: GAMES,
  [AchievementMetric.BlogsHosted]: ["блог", "блога", "блогов"],
  [AchievementMetric.PublicationsAuthored]: [
    "публикация",
    "публикации",
    "публикаций",
  ],
  [AchievementMetric.TopicsAuthored]: ["топик", "топика", "топиков"],
  [AchievementMetric.CommentsAuthored]: [
    "комментарий",
    "комментария",
    "комментариев",
  ],
  [AchievementMetric.GlobalChatMessages]: [
    "сообщение",
    "сообщения",
    "сообщений",
  ],
  [AchievementMetric.LikesReceived]: [
    "отметка Нравится",
    "отметки Нравится",
    "отметок Нравится",
  ],
  [AchievementMetric.BansReceived]: ["бан", "бана", "банов"],
};

/** A tier's threshold with its unit word; a new metric changes getMetricValue too. */
export function formatThreshold(
  metric: AchievementMetric,
  threshold: number,
): string {
  if (metric === AchievementMetric.DaysSinceRegistration) {
    const years = metricDisplayNumber(metric, threshold);
    return `${years} ${pluralize(years, "год", "года", "лет")}`;
  }
  // No "+" sign: a signed goal reads broken in "350 из +500".
  if (metric === AchievementMetric.PostReviewScoreSum) {
    return `${threshold} рейтинга`;
  }
  // A metric the server added before this client knew it is shown as the bare number.
  const words = UNIT_WORDS[metric];
  return words
    ? `${threshold} ${pluralize(threshold, ...words)}`
    : String(threshold);
}

/** "23-й литературный конкурс": the title of a placement award's tile. */
export function formatContestSeriesTitle(
  contestType: ContestType,
  number: number,
): string {
  const ordinal = `${number}-й`;
  if (contestType === ContestType.Literary) {
    return `${ordinal} литературный конкурс`;
  }
  // Art and future types.
  return `${ordinal} арт-конкурс`;
}
