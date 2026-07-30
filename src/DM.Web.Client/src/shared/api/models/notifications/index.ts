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

/**
 * The event a notification is about, spelled the way the server spells it.
 *
 * The value is the member name because that is what arrives. Both transports
 * write the event through JsonStringEnumConverter now — the notification list
 * over REST and the hub over the socket — so `eventType` reads "NewMessage"
 * whichever one delivered it. It used to be a number here, and a second numeric
 * copy lived in a sibling file where NewPoll had drifted to 51: the server's
 * slot for DeletedPublicationComment. Names cannot drift the way numbers did,
 * and one table cannot disagree with itself.
 *
 * Only events the client acts on are listed. A member no screen reads is a name
 * kept in step with nothing, and every member here is held to a real server
 * event by NotificationVocabularyShould in DM.Architecture.Tests.
 *
 * @see src/DM.Domain.Core/Enums/EventType.cs
 */
export enum NotificationType {
  // Messaging
  NewMessage = "NewMessage",
  NewGlobalChatMessage = "NewGlobalChatMessage",

  // Community
  UserAvatarChanged = "UserAvatarChanged",
  UserMentioned = "UserMentioned",

  // Blog
  NewPublication = "NewPublication",
  LikedPublication = "LikedPublication",
  NewBlogComment = "NewBlogComment",
  LikedBlogComment = "LikedBlogComment",
  BlogInvitationCreated = "BlogInvitationCreated",
  BlogInvitationAccepted = "BlogInvitationAccepted",
  BlogInvitationRejected = "BlogInvitationRejected",

  // Subscriptions
  NewTopicInSubscribedBoard = "NewTopicInSubscribedBoard",
  NewCommentInSubscribedTopic = "NewCommentInSubscribedTopic",
  NewGameFromSubscribedAuthor = "NewGameFromSubscribedAuthor",
  NewPostInSubscribedGame = "NewPostInSubscribedGame",
  NewTopicFromSubscribedAuthor = "NewTopicFromSubscribedAuthor",
  NewBlogFromSubscribedAuthor = "NewBlogFromSubscribedAuthor",

  // Forum. Spelled the way the server spells them: the two halves of the title
  // table are paired by member name, and an event that is a "forum topic" here
  // and a "topic" there cannot be paired at all.
  NewTopic = "NewTopic",
  LikedTopic = "LikedTopic",
  NewTopicComment = "NewTopicComment",
  LikedTopicComment = "LikedTopicComment",

  // Games
  NewGame = "NewGame",
  NewCharacter = "NewCharacter",
}

export type NotificationId = Id<string>;
export type UserNotification = {
  id: Served<NotificationId>;
  eventType: Served<NotificationType>;
  payload: Served<any>;
};

/**
 * One notification as the hub pushes it: the same three fields the list returns
 * over REST, in the same JSON. It used to live in a file of its own next to a
 * second copy of the event vocabulary, which is how the two came apart.
 */
export interface SignalRNotification {
  id: string;
  eventType: NotificationType;
  payload: Record<string, unknown>;
}

/** Notification handler callback type */
export type NotificationHandler = (notification: SignalRNotification) => void;

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
