import type { Envelope, ListEnvelope } from "../common";

/**
 * Метрика прогресса для достижений. SSOT: серверный enum
 * `DM.Domain.Core.Enums.AchievementMetric`. При добавлении новой метрики
 * правится оба места + `getMetricValue` на FE и `AchievementMetricResolver.GetValue` на BE.
 *
 * Сериализуется бэкендом как строка (глобальный JsonStringEnumConverter).
 */
export enum AchievementMetric {
  GamePostsAuthored = "GamePostsAuthored",
  DaysSinceRegistration = "DaysSinceRegistration",
  PostReviewScoreSum = "PostReviewScoreSum",
  GamesHosted = "GamesHosted",
  GamesPlayed = "GamesPlayed",
  BlogsHosted = "BlogsHosted",
  TopicsAuthored = "TopicsAuthored",
  CommentsAuthored = "CommentsAuthored",
  GlobalChatMessages = "GlobalChatMessages",
  BansReceived = "BansReceived",
  GameDrops = "GameDrops",
  PublicationsAuthored = "PublicationsAuthored",
  LikesReceived = "LikesReceived",
}

/** Тип конкурса. У каждого типа своя сквозная нумерация. */
export enum ContestType {
  Literary = "Literary",
  Art = "Art",
}

/**
 * Категория достижений (цепочка тиров одной метрики). SSOT для иконки,
 * описания и порядка отображения.
 */
export interface AchievementCategory {
  id: string;
  code: string;
  title: string;
  /** Описание метрики (что именно считается). Показывается в шапке popover-а. */
  description: string;
  iconName: string;
  metric: AchievementMetric;
  sortOrder: number;
  isActive: boolean;
}

/** Тир достижения. Категория-родитель включена в навигацию. */
export interface AchievementType {
  id: string;
  code: string;
  title: string;
  threshold: number;
  tier: number | null;
  category: AchievementCategory;
}

/** Факт получения достижения пользователем. */
export interface UserAchievement {
  id: string;
  type: AchievementType;
  earnedUtc: string;
}

/** Тип награды (timeless каталог). */
export interface AwardType {
  id: string;
  code: string;
  title: string;
  /** Полная фраза («Победитель конкурса», «Лучшая работа по голосованию»). */
  description: string;
  iconName: string;
  /** Визуальный тир: 1=gold, 2=silver, 3=bronze. */
  tier: number | null;
  sortOrder: number;
  isActive: boolean;
}

/** Серия конкурса. */
export interface ContestSeries {
  id: string;
  contestType: ContestType;
  /** Сквозной номер в рамках типа (Literary 21, 22, 23…). */
  number: number;
  /** Год проведения (отображается в year-бейдже на тайле). */
  year: number;
  /** Ссылка на форумный топик с итогами (опционально). */
  topicUrl: string | null;
  isActive: boolean;
}

/** Награда, выданная пользователю. */
export interface UserAward {
  id: string;
  type: AwardType;
  /** Серия конкурса (опц.). Содержит number/year/topicUrl. */
  contestSeries: ContestSeries | null;
  /** Ссылка на топик с самой работой, за которую получена награда (опц). */
  workUrl: string | null;
  awardedUtc: string;
}

// ---- Admin request DTOs ----

export interface UpdateAchievementCategoryRequest {
  title?: string | null;
  description?: string | null;
  iconName?: string | null;
  sortOrder?: number | null;
  isActive?: boolean | null;
}

export interface CreateAchievementTypeRequest {
  code: string;
  title: string;
  threshold: number;
  tier?: number | null;
  achievementCategoryId: string;
}

export interface UpdateAchievementTypeRequest {
  title?: string | null;
  threshold?: number | null;
  tier?: number | null;
}

export interface CreateAwardTypeRequest {
  code: string;
  title: string;
  description: string;
  iconName: string;
  tier?: number | null;
  sortOrder?: number;
}

export interface UpdateAwardTypeRequest {
  title?: string | null;
  description?: string | null;
  iconName?: string | null;
  tier?: number | null;
  sortOrder?: number | null;
  isActive?: boolean | null;
}

export interface CreateContestSeriesRequest {
  contestType: ContestType;
  number: number;
  year: number;
  topicUrl?: string | null;
}

export interface UpdateContestSeriesRequest {
  contestType?: ContestType | null;
  number?: number | null;
  year?: number | null;
  topicUrl?: string | null;
  isActive?: boolean | null;
}

export interface GrantUserAwardRequest {
  awardTypeId: string;
  contestSeriesId?: string | null;
  workUrl?: string | null;
}

// ---- Response envelope shortcuts ----

export type AwardTypesResponse = ListEnvelope<AwardType>;
export type UserAwardsResponse = ListEnvelope<UserAward>;
export type AchievementTypesResponse = ListEnvelope<AchievementType>;
export type AchievementCategoriesResponse = ListEnvelope<AchievementCategory>;
export type ContestSeriesResponse = ListEnvelope<ContestSeries>;
export type UserAchievementsResponse = ListEnvelope<UserAchievement>;
export type AwardTypeEnvelope = Envelope<AwardType>;
export type UserAwardEnvelope = Envelope<UserAward>;
export type AchievementTypeEnvelope = Envelope<AchievementType>;
export type AchievementCategoryEnvelope = Envelope<AchievementCategory>;
export type ContestSeriesEnvelope = Envelope<ContestSeries>;
