import { AchievementMetric } from "@/shared/api/models/achievements";
import type { User } from "@/shared/api/models/community";

/**
 * SSOT-маппинг метрики → значение из профиля пользователя.
 *
 * Парный resolver на бэкенде — `AchievementMetricResolver.GetValue`.
 * При добавлении новой метрики обе функции меняются вместе.
 *
 * Все источники — денормализованные счетчики на профиле, кроме
 * `DaysSinceRegistration`, которая вычисляется из `registeredUtc` на
 * лету (одно вычитание, без сетевых запросов).
 */
export function getMetricValue(metric: AchievementMetric, user: User): number {
  switch (metric) {
    case AchievementMetric.GamePostsAuthored:
      // Та же метрика, что правило «новичок до 100 постов» на backend
      // (поле User.QuantityRating).
      return user.rating?.totalPosts ?? 0;
    case AchievementMetric.DaysSinceRegistration:
      return daysSinceRegistration(user);
    case AchievementMetric.PostReviewScoreSum:
      // Знаковый score; отрицательное обнуляем — пороги задаются
      // положительными числами, отрицательный счет их не достигнет.
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
      // «Резиновая уточка» — пасхалка на мем про утят-террористов.
      // Считаем количество выданных банов (исключая soft-deleted).
      return user.bansReceived ?? 0;
    case AchievementMetric.GameDrops:
      // «Дропы» — игры, которые пользователь покинул добровольно
      // (Retired-character + IsPlayerLeft). Смерть и изгнание не считаются.
      return user.gameDrops ?? 0;
    case AchievementMetric.PublicationsAuthored:
      // «Публикации» — статьи в блогах. Драфты тоже считаются.
      return user.publicationsAuthored ?? 0;
    case AchievementMetric.LikesReceived:
      // «Лайки» — суммарные лайки на топиках, публикациях, комментариях
      // и чат-сообщениях. Игровые посты идут через «Рейтинг».
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
