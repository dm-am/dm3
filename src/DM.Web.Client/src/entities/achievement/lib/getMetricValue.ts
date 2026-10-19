import { AchievementMetric } from "@/shared/api/models/achievements";
import type { User } from "@/shared/api/models/community";

/** A metric's value off the profile, paired with the server's AchievementMetricResolver. */
export function getMetricValue(metric: AchievementMetric, user: User): number {
  switch (metric) {
    case AchievementMetric.GamePostsAuthored:
      return user.rating?.totalPosts ?? 0;
    case AchievementMetric.DaysSinceRegistration:
      return daysSinceRegistration(user);
    case AchievementMetric.PostReviewScoreSum:
      // Thresholds are positive, so a negative score counts as zero.
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
      return user.bansReceived ?? 0;
    case AchievementMetric.GameDrops:
      // Games left voluntarily: death and exile do not count.
      return user.gameDrops ?? 0;
    case AchievementMetric.PublicationsAuthored:
      // Drafts count too.
      return user.publicationsAuthored ?? 0;
    case AchievementMetric.LikesReceived:
      // Game posts are counted by the rating instead.
      return user.likesReceived ?? 0;
    default:
      return 0;
  }
}

function daysSinceRegistration(user: User): number {
  const reg = user.registeredUtc;
  if (!reg) return 0;
  const ts = new Date(reg).getTime();
  if (Number.isNaN(ts)) return 0;
  const days = Math.floor((Date.now() - ts) / (1000 * 60 * 60 * 24));
  return Math.max(0, days);
}
