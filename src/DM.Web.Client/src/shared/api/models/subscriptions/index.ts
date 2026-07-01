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
 * The same bit positions carry different semantics depending on the
 * subscription's `targetType`:
 * - Game / Blog / Topic targets — per-entity content flags
 *   (`NewPosts`, `NewPublications`, `NewComments`, `StatusChanges`,
 *   `CharacterUpdates`).
 * - User target — per-author-category flags (`AuthorGameEvents`,
 *   `AuthorBlogEvents`, `AuthorTopicEvents`), gated by the three
 *   checkboxes in the subscribe popover.
 *
 * Channel flags (`InApp`, `Email`) are orthogonal and apply to all
 * target types. Bit 6 is intentionally left vacant — it used to hold
 * the now-removed `AuthorNewContent` catch-all.
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
