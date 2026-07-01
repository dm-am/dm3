import {
  AchievementMetric,
  ContestType,
} from "@/shared/api/models/achievements";

/**
 * Человекочитаемый порог для тира. SSOT для всех мест, где показываются
 * пороги (tile-цифры под bar, popover-таблица тиров).
 *
 * Описание метрики хранится на категории; здесь — только формат числа
 * под единицу измерения метрики (постов / лет / лайков / etc).
 *
 * При добавлении новой метрики правится здесь + парный case в
 * `getMetricValue` (он считает значение метрики у конкретного юзера).
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
      return `${threshold} ${plural(threshold, "пост", "поста", "постов")}`;
    case AchievementMetric.DaysSinceRegistration: {
      const years = metricDisplayNumber(metric, threshold);
      return `${years} ${plural(years, "год", "года", "лет")}`;
    }
    case AchievementMetric.PostReviewScoreSum:
      return `+${threshold}`;
    case AchievementMetric.GamesHosted:
    case AchievementMetric.GamesPlayed:
    case AchievementMetric.GameDrops:
      return `${threshold} ${plural(threshold, "игра", "игры", "игр")}`;
    case AchievementMetric.BlogsHosted:
      return `${threshold} ${plural(threshold, "блог", "блога", "блогов")}`;
    case AchievementMetric.PublicationsAuthored:
      return `${threshold} ${plural(threshold, "публикация", "публикации", "публикаций")}`;
    case AchievementMetric.TopicsAuthored:
      return `${threshold} ${plural(threshold, "топик", "топика", "топиков")}`;
    case AchievementMetric.CommentsAuthored:
      return `${threshold} ${plural(threshold, "комментарий", "комментария", "комментариев")}`;
    case AchievementMetric.GlobalChatMessages:
      return `${threshold} ${plural(threshold, "сообщение", "сообщения", "сообщений")}`;
    case AchievementMetric.LikesReceived:
      return `${threshold} ${plural(threshold, "лайк", "лайка", "лайков")}`;
    case AchievementMetric.BansReceived:
      return `${threshold} ${plural(threshold, "бан", "бана", "банов")}`;
    default:
      return String(threshold);
  }
}

/**
 * Русские склонения числительных: 1 год / 2 года / 5 лет.
 * Учитывает «11 лет» (не «11 год»), «21 год» (не «21 лет»), etc.
 */
function plural(n: number, one: string, few: string, many: string): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod100 >= 11 && mod100 <= 14) return many;
  if (mod10 === 1) return one;
  if (mod10 >= 2 && mod10 <= 4) return few;
  return many;
}

/**
 * Лейбл серии конкурса для тайла и popover'а:
 *   «23-й литературный конкурс», «1-й арт конкурс».
 * Используется как title тайла награды-места (contest_first/second/third)
 * — место читается по tier-color, а контекст «какой конкурс» — здесь.
 */
export function formatContestSeriesTitle(
  contestType: ContestType,
  number: number,
): string {
  const ordinal = `${number}-й`;
  if (contestType === ContestType.Literary) {
    return `${ordinal} литературный конкурс`;
  }
  // Art и будущие типы.
  return `${ordinal} арт конкурс`;
}
