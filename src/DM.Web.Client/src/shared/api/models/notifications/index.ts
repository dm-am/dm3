import type { Id, Served } from "../common";

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
  GlobalChatEventStarted = "GlobalChatEventStarted",
  GlobalChatEventEnded = "GlobalChatEventEnded",

  // Community
  UserAvatarChanged = "UserAvatarChanged",

  // Blog
  NewPublication = "NewPublication",
  LikedPublication = "LikedPublication",
  NewBlogComment = "NewBlogComment",
  NewPublicationComment = "NewPublicationComment",
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

  // Security. These reach the owner of the account and nobody else, and they
  // carry no link: what a suspicious login or a changed password points at is
  // not a page.
  PasswordChanged = "PasswordChanged",
  EmailChanged = "EmailChanged",
  SuspiciousLoginActivity = "SuspiciousLoginActivity",
  AccountLocked = "AccountLocked",
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
