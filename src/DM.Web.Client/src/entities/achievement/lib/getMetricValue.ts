import { AchievementMetric } from "@/shared/api/models/achievements";
import type { User } from "@/shared/api/models/community";

/**
 * SSOT mapping of a metric → value from the user profile.
 *
 * The paired backend resolver is `AchievementMetricResolver.GetValue`.
 * When adding a new metric both functions change together.
 *
 * All sources are denormalized profile counters, except
 * `DaysSinceRegistration`, which is computed from `registeredUtc` on
 * the fly (a single subtraction, no network requests).
 */
export function getMetricValue(metric: AchievementMetric, user: User): number {
  switch (metric) {
    case AchievementMetric.GamePostsAuthored:
      // The same metric as the "новичок до 100 постов" rule on the backend
      // (the User.QuantityRating field).
      return user.rating?.totalPosts ?? 0;
    case AchievementMetric.DaysSinceRegistration:
      return daysSinceRegistration(user);
    case AchievementMetric.PostReviewScoreSum:
      // Signed score; negatives are clamped to zero — thresholds are defined
      // as positive numbers, a negative score will never reach them.
      return Math.max(0, user.rating?.postReviewScoreSum ?? 0);
    case AchievementMetric.GamesHosted:
      return user.gamesHosting ?? 0;
    case AchievementMetric.GamesPlayed:
      return user.gamesPlaying ?? 0;
    case AchievementMetric.BlogsHosted:
      return user.blogsHosting ?? 0;
    case AchievementMetric.TopicsAuthored:
      return user.topicsAuthored ?? 0;
    case AchievementMetric.CommentsAuthored:
      return user.commentsAuthored ?? 0;
    case AchievementMetric.GlobalChatMessages:
      return user.globalChatMessages ?? 0;
    case AchievementMetric.BansReceived:
      // "Резиновая уточка" — an easter egg for the duckling-terrorists meme.
      // Count the bans issued (excluding soft-deleted).
      return user.bansReceived ?? 0;
    case AchievementMetric.GameDrops:
      // "Дропы" — games the user left voluntarily
      // (Retired character + IsPlayerLeft). Death and exile do not count.
      return user.gameDrops ?? 0;
    case AchievementMetric.PublicationsAuthored:
      // "Публикации" — blog articles. Drafts count too.
      return user.publicationsAuthored ?? 0;
    case AchievementMetric.LikesReceived:
      // "Лайки" — total likes on topics, publications, comments
      // and chat messages. Game posts go through "Рейтинг".
      return user.likesReceived ?? 0;
    default:
      return 0;
  }
}

function daysSinceRegistration(user: User): number {
  const reg = user.registeredUtc ?? user.registrationUtc;
  if (!reg) return 0;
  const ts = new Date(reg).getTime();
  if (Number.isNaN(ts)) return 0;
  const days = Math.floor((Date.now() - ts) / (1000 * 60 * 60 * 24));
  return Math.max(0, days);
}
