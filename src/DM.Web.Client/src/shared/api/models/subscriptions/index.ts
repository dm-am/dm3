import type { ModuleStatus } from "../common";

// Subscription target types
export enum SubscriptionTargetType {
  Game = 1,
  Blog = 2,
  Topic = 3,
  User = 4,
}

/**
 * Subscription notification settings (bit flags).
 *
 * The three groups occupy disjoint bit ranges: no position ever carries two
 * meanings, so a value is readable without knowing the `targetType`.
 * - Bits 0-5 — per-entity content flags (`NewPosts`, `NewPublications`,
 *   `NewComments`, `NewTopics`, `StatusChanges`, `CharacterUpdates`), read
 *   for Game / Blog / Topic targets.
 * - Bits 7-8 — channel flags (`InApp`, `Email`), orthogonal and read for
 *   every target type.
 * - Bits 9-11 — per-author-category flags (`AuthorGameEvents`,
 *   `AuthorBlogEvents`, `AuthorTopicEvents`), read for the User target and
 *   surfaced as the three checkboxes in the subscribe popover.
 *
 * Bit 6 stays vacant: it held the now-removed `AuthorNewContent` catch-all
 * and is not recycled, so no bit ever means two things.
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

  // 1 << 6 vacant (removed AuthorNewContent — replaced by the three
  // Author*Events flags below).

  // Channels
  InApp = 1 << 7,
  Email = 1 << 8,

  // Per-author-category flags (User target only)
  AuthorGameEvents = 1 << 9,
  AuthorBlogEvents = 1 << 10,
  AuthorTopicEvents = 1 << 11,

  // Presets
  GameReaderDefault = (1 << 0) | (1 << 4) | (1 << 7),
  GamePlayerDefault = (1 << 0) | (1 << 4) | (1 << 5) | (1 << 7) | (1 << 8),
  BlogReaderDefault = (1 << 1) | (1 << 7),
  TopicDefault = (1 << 2) | (1 << 7),
  UserSubscriptionDefault = (1 << 9) | (1 << 10) | (1 << 11) | (1 << 7),
}

// Subscription DTO
export interface Subscription {
  id: string;
  targetType: SubscriptionTargetType;
  targetId: string;
  /**
   * Name of what was subscribed to, resolved server-side. Absent when the
   * target is gone. Without it the page had nothing but the identifier to
   * print, and printed it: a column of GUIDs.
   */
  targetTitle?: string;
  /** Username of a User target — the profile route is addressed by name. */
  targetUsername?: string;
  /**
   * Status of a Game or Blog target, resolved server-side: the page names a
   * game or a blog in the colour of its status, as the site does everywhere.
   * Absent for a topic, a user and a target that is gone.
   */
  targetStatus?: ModuleStatus;
  /**
   * When a Game or Blog target was first activated (ISO date string): what a
   * "new" game or blog is counted from, as activatedUtc of GameRef and
   * BlogRef. Absent for a topic, a user, a target that is gone and one never
   * activated.
   */
  targetActivatedUtc?: string;
  settings: SubscriptionSettings;
  createdUtc: string;
}

// Subscribe request
export interface SubscribeRequest {
  targetType: SubscriptionTargetType;
  targetId: string;
  settings?: SubscriptionSettings;
}

// Update subscription request
export interface UpdateSubscriptionRequest {
  settings: SubscriptionSettings;
}
