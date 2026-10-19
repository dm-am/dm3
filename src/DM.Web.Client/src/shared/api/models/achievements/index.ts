import type { UserRef } from "../common";

/**
 * Mirrors DM.Domain.Core.Enums.AchievementMetric, sent as a string. A new
 * metric also changes getMetricValue here and AchievementMetricResolver there.
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

/** Contest type. Each type has its own sequential numbering. */
export enum ContestType {
  Literary = "Literary",
  Art = "Art",
}

/** A chain of tiers for one metric; holds the icon, description and order. */
export interface AchievementCategory {
  id: string;
  code: string;
  title: string;
  /** Metric description (what exactly is counted). Shown in the popover header. */
  description: string;
  iconName: string;
  metric: AchievementMetric;
  sortOrder: number;
  isActive: boolean;
}

/** Achievement tier. The parent category is included in the navigation. */
export interface AchievementType {
  id: string;
  code: string;
  title: string;
  threshold: number;
  tier: number | null;
  category: AchievementCategory;
}

/** The fact that a user earned an achievement. */
export interface UserAchievement {
  id: string;
  type: AchievementType;
  earnedUtc: string;
}

/** Award type (timeless catalog). */
export interface AwardType {
  id: string;
  code: string;
  title: string;
  /** What the award is granted for. Shown in the popover. */
  description: string;
  iconName: string;
  /** Visual tier: 1=gold, 2=silver, 3=bronze. */
  tier: number | null;
  sortOrder: number;
  isActive: boolean;
}

export interface ContestSeries {
  id: string;
  contestType: ContestType;
  /** Sequential number within the type (Literary 21, 22, 23…). */
  number: number;
  /** Year held (shown in the year badge on the tile). */
  year: number;
  /** Link to the forum topic with results (optional). */
  topicUrl: string | null;
  isActive: boolean;
}

/** An award granted to a user. */
export interface UserAward {
  id: string;
  type: AwardType;
  /** Contest series (optional). Contains number/year/topicUrl. */
  contestSeries: ContestSeries | null;
  /** Link to the topic with the work the award was granted for (optional). */
  workUrl: string | null;
  awardedUtc: string;
}

/**
 * An award within a contest series, with its recipient: UserAward comes only
 * inside one user's list and has none.
 */
export interface ContestSeriesAward {
  id: string;
  user: UserRef;
  type: AwardType;
  workUrl: string | null;
  awardedUtc: string;
}

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
