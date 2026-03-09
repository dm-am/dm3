// Subscription target types
export enum SubscriptionTargetType {
  Game = 1,
  Blog = 2,
  Topic = 3,
  User = 4,
}

// Subscription notification settings (flags)
export enum SubscriptionSettings {
  None = 0,
  NewPosts = 1 << 0,
  NewPublications = 1 << 1,
  NewComments = 1 << 2,
  NewTopics = 1 << 3,
  StatusChanges = 1 << 4,
  CharacterUpdates = 1 << 5,
  AuthorNewContent = 1 << 6,
  InApp = 1 << 7,
  Email = 1 << 8,
  // Presets
  GameReaderDefault = (1 << 0) | (1 << 4) | (1 << 7),
  GamePlayerDefault = (1 << 0) | (1 << 4) | (1 << 5) | (1 << 7) | (1 << 8),
  BlogReaderDefault = (1 << 1) | (1 << 7),
  TopicDefault = (1 << 2) | (1 << 7),
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
