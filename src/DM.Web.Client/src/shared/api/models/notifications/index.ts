import type { Served } from "../common";

/**
 * The event a notification is about, by member name, which both transports
 * send (JsonStringEnumConverter). Only events the client acts on; each is held
 * to a server event by NotificationVocabularyShould.
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
  NewCommentInSubscribedTopic = "NewCommentInSubscribedTopic",
  NewGameFromSubscribedAuthor = "NewGameFromSubscribedAuthor",
  NewPostInSubscribedGame = "NewPostInSubscribedGame",
  NewTopicFromSubscribedAuthor = "NewTopicFromSubscribedAuthor",
  NewBlogFromSubscribedAuthor = "NewBlogFromSubscribedAuthor",

  // Forum. Spelled as the server spells them: the title table pairs by name.
  NewTopic = "NewTopic",
  LikedTopic = "LikedTopic",
  NewTopicComment = "NewTopicComment",
  LikedTopicComment = "LikedTopicComment",

  // Games
  NewGame = "NewGame",
  NewCharacter = "NewCharacter",

  // Moderation: these reach the person the decision was about; the closing
  // of a ticket reaches its author, who has an account.
  WarningIssued = "WarningIssued",
  WarningPointsChanged = "WarningPointsChanged",
  BanIssued = "BanIssued",
  TicketResolved = "TicketResolved",

  // Security: the owner of the account only, and no link.
  PasswordChanged = "PasswordChanged",
  EmailChanged = "EmailChanged",
  SuspiciousLoginActivity = "SuspiciousLoginActivity",
  AccountLocked = "AccountLocked",
}

type NotificationId = Served<string>;

/**
 * One field of a notification, in the words of the letter about the event. A
 * moment is an ISO string for the reader's clock; isDestination, not the
 * order, marks the place it leads to.
 */
export type NotificationDetail = {
  label: Served<string>;
  value: Served<string>;
  isMoment: Served<boolean>;
  isDestination: Served<boolean>;
};

/**
 * One row of the notification list, built from the table the letters are
 * written from. url is a path from the root, absent when there is nothing to
 * lead to (WhenWritingNull).
 */
export type UserNotification = {
  id: Served<NotificationId>;
  eventType: Served<NotificationType>;
  payload: Served<Record<string, unknown>>;
  createdUtc: Served<string>;
  isRead: Served<boolean>;
  title: Served<string>;
  url?: Served<string>;
  details: Served<NotificationDetail[]>;
};

/** One notification as the hub pushes it: the list's DTO, always unread. */
export interface SignalRNotification {
  id: string;
  eventType: NotificationType;
  payload: Record<string, unknown>;
}

/** Notification handler callback type */
export type NotificationHandler = (notification: SignalRNotification) => void;
