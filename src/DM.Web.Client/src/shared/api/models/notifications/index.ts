import type {
  Id,
  Served,
  Username,
  TopicId,
  BoardId,
  GameId,
  BlogId,
  PublicationId,
} from "../common";

export enum NotificationType {
  // Blog notifications (41-52)
  NewPublication = 41,
  LikedPublication = 44,
  NewBlogComment = 45,
  LikedBlogComment = 48,
  BlogInvitationCreated = 53,
  BlogInvitationAccepted = 54,
  BlogInvitationRejected = 55,

  // Subscription notifications (71-79)
  NewTopicInSubscribedBoard = 71,
  NewCommentInSubscribedTopic = 72,
  NewGameFromSubscribedAuthor = 73,
  NewPostInSubscribedGame = 74,
  UserMentioned = 75,
  // 76 vacated (was NewPublicationFromSubscribedAuthor — per-publication
  // notifications for user subscriptions were dropped in favor of
  // blog-level signals).
  NewTopicFromSubscribedAuthor = 77,
  NewBlogFromSubscribedAuthor = 78,

  // Forum notifications (101-114). Spelled the way the server spells them:
  // the two halves of the title table are paired by member name, and an event
  // that is a "forum topic" here and a "topic" there cannot be paired at all.
  NewTopic = 101,
  LikedTopic = 104,
  NewTopicComment = 111,
  LikedTopicComment = 114,

  // Game notifications (301+)
  NewGame = 301,
  NewCharacter = 361,
}

export type NotificationId = Id<string>;
export type UserNotification = {
  id: Served<NotificationId>;
  eventType: Served<NotificationType>;
  payload: Served<any>;
};

export type NewCharacterData = {
  authorUsername: Served<Username>;
  gameTitle: Served<string>;
  gameId: Served<GameId>;
};

export type TopicLikedData = {
  authorUsername: Served<Username>;
  topicTitle: Served<string>;
  topicId: Served<TopicId>;
};

// Subscription notification payloads
export type NewGameFromSubscribedAuthorData = {
  gameId: Served<GameId>;
  gameTitle: Served<string>;
  authorUsername: Served<Username>;
};

export type NewBlogFromSubscribedAuthorData = {
  blogId: Served<BlogId>;
  blogTitle: Served<string>;
  authorUsername: Served<Username>;
};

export type NewTopicFromSubscribedAuthorData = {
  topicId: Served<TopicId>;
  topicTitle: Served<string>;
  boardId: Served<BoardId>;
  boardTitle: Served<string>;
  authorUsername: Served<Username>;
};

export type NewPostInSubscribedGameData = {
  postId: Served<string>;
  gameId: Served<GameId>;
  gameTitle: Served<string>;
  roomTitle: Served<string>;
  authorUsername: Served<Username>;
  characterName: Served<string | null>;
};

export type NewCommentInSubscribedTopicData = {
  commentId: Served<string>;
  topicId: Served<TopicId>;
  topicTitle: Served<string>;
  authorUsername: Served<Username>;
};

export type UserMentionedData = {
  entityId: Served<string>;
  entityType: Served<string>;
  authorUsername: Served<Username>;
  contextTitle: Served<string>;
};

// Blog invitation notification payload
export type BlogInvitationData = {
  blogId: Served<BlogId>;
  blogTitle: Served<string>;
  inviterUsername: Served<Username>;
  role: Served<string>;
};

// Liked content notification payloads
export type PublicationLikedData = {
  likerUsername: Served<Username>;
  publicationId: Served<PublicationId>;
  publicationTitle: Served<string>;
  blogId: Served<BlogId>;
  blogTitle: Served<string>;
};

export type ForumCommentLikedData = {
  likerUsername: Served<Username>;
  topicId: Served<TopicId>;
  topicTitle: Served<string>;
};

// Notification count
export type NotificationCount = {
  count: number;
};

// Notification category for bot delivery preferences
export enum NotificationCategory {
  Messages = 1,
  Forum = 2,
  Games = 3,
  Subscriptions = 4,
  Security = 5,
  Moderation = 6,
  Blog = 7,
}

// Per-channel bot connection and notification settings
export type BotConnection = {
  connected: boolean;
  enabled: boolean;
  enabledCategories: NotificationCategory[];
};

// Notification delivery settings
export type NotificationSettings = {
  discord?: BotConnection | null;
  telegram?: BotConnection | null;
};

// Update bot connection request
export type UpdateBotConnection = {
  enabled?: boolean | null;
  enabledCategories?: NotificationCategory[] | null;
};

// Update notification settings request
export type UpdateNotificationSettingsRequest = {
  discord?: UpdateBotConnection | null;
  telegram?: UpdateBotConnection | null;
};

// Backwards compatibility aliases
export type NotificationPreferences = NotificationSettings;
