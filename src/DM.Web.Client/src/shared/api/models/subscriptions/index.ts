import type { ModuleStatus } from "../common";

export enum SubscriptionTargetType {
  Game = 1,
  Blog = 2,
  Topic = 3,
  User = 4,
}

/**
 * Bit flags in disjoint ranges, so a value reads without its targetType: 0-5
 * content (Game, Blog, Topic), 7-8 channels (every target), 9-11 author
 * categories (User). Bit 6 held the removed AuthorNewContent and stays vacant.
 */
export enum SubscriptionSettings {
  None = 0,

  // Per-entity content flags
  NewPosts = 1 << 0,
  NewPublications = 1 << 1,
  NewComments = 1 << 2,
  NewTopics = 1 << 3,
  StatusChanges = 1 << 4,
  CharacterUpdates = 1 << 5,

  // Channels
  InApp = 1 << 7,
  Email = 1 << 8,

  // Per-author-category flags (User target only)
  AuthorGameEvents = 1 << 9,
  AuthorBlogEvents = 1 << 10,
  AuthorTopicEvents = 1 << 11,

  // Presets
  GameReaderDefault = NewPosts | StatusChanges | InApp,
  GamePlayerDefault = NewPosts |
    StatusChanges |
    CharacterUpdates |
    InApp |
    Email,
  BlogReaderDefault = NewPublications | InApp,
  TopicDefault = NewComments | InApp,
  UserSubscriptionDefault = AuthorGameEvents |
    AuthorBlogEvents |
    AuthorTopicEvents |
    InApp,
}

export interface Subscription {
  id: string;
  targetType: SubscriptionTargetType;
  targetId: string;
  /** Resolved server-side; absent when the target is gone. */
  targetTitle?: string;
  /** Username of a User target — the profile route is addressed by name. */
  targetUsername?: string;
  /**
   * Of a Game or Blog target, for the colour of its status. Absent for a
   * topic, a user and a target that is gone.
   */
  targetStatus?: ModuleStatus;
  /**
   * When a Game or Blog target was first activated: what "new" counts from,
   * as activatedUtc of GameRef and BlogRef.
   */
  targetActivatedUtc?: string;
  settings: SubscriptionSettings;
  createdUtc: string;
}

/** A category of a subscription to a user: its bit, and what its notifications are about. */
export interface UserSubscriptionCategory {
  category: SubscriptionSettings;
  events: string;
}

export interface SubscribeRequest {
  targetType: SubscriptionTargetType;
  targetId: string;
  settings?: SubscriptionSettings;
}

export interface UpdateSubscriptionRequest {
  settings: SubscriptionSettings;
}
