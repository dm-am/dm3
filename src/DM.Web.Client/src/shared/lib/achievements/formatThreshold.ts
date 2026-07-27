import {
  AchievementMetric,
  ContestType,
} from "@/shared/api/models/achievements";
import { pluralize } from "@/shared/lib/utils/pluralize";

/**
 * Human-readable threshold for a tier. SSOT for every place thresholds
 * are shown (tile numbers under the bar, the popover tier table).
 *
 * The metric description lives on the category; here only the number format
 * for the metric's unit of measure (posts / years / likes / etc).
 *
 * When adding a new metric, this changes + the paired case in
 * `getMetricValue` (it computes the metric value for a specific user).
 */
/**
 * Display-unit number for a metric value. Most metrics are raw counts shown
 * as-is; DaysSinceRegistration is stored in days but shown in years, so it is
 * converted. SSOT used by BOTH the goal number (formatThreshold) and the
 * current-progress number — so "current из goal" never mixes units
 * (e.g. never "4054 из 15 лет", always "11 из 15 лет").
 */
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

export function formatThreshold(
  metric: AchievementMetric,
  threshold: number,
): string {
  switch (metric) {
    case AchievementMetric.GamePostsAuthored:
      return `${threshold} ${pluralize(threshold, "пост", "поста", "постов")}`;
    case AchievementMetric.DaysSinceRegistration: {
      const years = metricDisplayNumber(metric, threshold);
      return `${years} ${pluralize(years, "год", "года", "лет")}`;
    }
    case AchievementMetric.PostReviewScoreSum:
      // No "+" sign: the progress line composes "X из <threshold>", and a
      // signed goal reads broken ("350 из +500"). Unit word instead.
      return `${threshold} рейтинга`;
    case AchievementMetric.GamesHosted:
    case AchievementMetric.GamesPlayed:
    case AchievementMetric.GameDrops:
      return `${threshold} ${pluralize(threshold, "игра", "игры", "игр")}`;
    case AchievementMetric.BlogsHosted:
      return `${threshold} ${pluralize(threshold, "блог", "блога", "блогов")}`;
    case AchievementMetric.PublicationsAuthored:
      return `${threshold} ${pluralize(threshold, "публикация", "публикации", "публикаций")}`;
    case AchievementMetric.TopicsAuthored:
      return `${threshold} ${pluralize(threshold, "топик", "топика", "топиков")}`;
    case AchievementMetric.CommentsAuthored:
      return `${threshold} ${pluralize(threshold, "комментарий", "комментария", "комментариев")}`;
    case AchievementMetric.GlobalChatMessages:
      return `${threshold} ${pluralize(threshold, "сообщение", "сообщения", "сообщений")}`;
    case AchievementMetric.LikesReceived:
      return `${threshold} ${pluralize(threshold, "лайк", "лайка", "лайков")}`;
    case AchievementMetric.BansReceived:
      return `${threshold} ${pluralize(threshold, "бан", "бана", "банов")}`;
    default:
      return String(threshold);
  }
}

/**
 * Contest series label for the tile and popover:
 *   "23-й литературный конкурс", "1-й арт конкурс".
 * Used as the tile title of a placement award (contest_first/second/third)
 * — the placement is read from the tier color, and the "which contest" context is here.
 */
export function formatContestSeriesTitle(
  contestType: ContestType,
  number: number,
): string {
  const ordinal = `${number}-й`;
  if (contestType === ContestType.Literary) {
    return `${ordinal} литературный конкурс`;
  }
  // Art and future types.
  return `${ordinal} арт конкурс`;
}
