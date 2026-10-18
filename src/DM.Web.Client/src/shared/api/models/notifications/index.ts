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

  // Moderation. These reach the person the decision was about: the warning
  // leads to the log entry that says why, the ban to the profile that says
  // until when. A warning is never withdrawn, only corrected, and the
  // correction is announced on its own: the points it costs the reader change
  // and the log gains a second signature saying why. The closing of a ticket
  // reaches its author, who has an account (a guest gets no letters), and
  // leads to the page of the ticket, where its conversation stands.
  WarningIssued = "WarningIssued",
  WarningPointsChanged = "WarningPointsChanged",
  BanIssued = "BanIssued",
  TicketResolved = "TicketResolved",

  // Security. These reach the owner of the account and nobody else, and they
  // carry no link: what a suspicious login or a changed password points at is
  // not a page.
  PasswordChanged = "PasswordChanged",
  EmailChanged = "EmailChanged",
  SuspiciousLoginActivity = "SuspiciousLoginActivity",
  AccountLocked = "AccountLocked",
}

export type NotificationId = Id<string>;

/**
 * One field of a notification under its title, in the words of the letter about
 * the same event. A moment is the ISO string the generator wrote, left for the
 * reader's clock to format like every date of the site.
 */
export type NotificationDetail = {
  label: Served<string>;
  value: Served<string>;
  isMoment: Served<boolean>;
};

/**
 * One row of the notification list. The title, the path and the details come
 * from the table the letter and the bots are written from, so a page showing
 * the list need not name an event or build an address of its own. `url` is a
 * path from the root of the site, and absent rather than null when there is
 * nothing to lead to: the API leaves null fields out (WhenWritingNull).
 */
export type UserNotification = {
  id: Served<NotificationId>;
  eventType: Served<NotificationType>;
  payload: Served<any>;
  createdUtc: Served<string>;
  isRead: Served<boolean>;
  title: Served<string>;
  url?: Served<string>;
  details: Served<NotificationDetail[]>;
};

/**
 * One notification as the hub pushes it. The push is the DTO the list returns,
 * written by the same mapper and always unread, and the screens that react to a
 * push read these three fields of it. It used to live in a file of its own next
 * to a second copy of the event vocabulary, which is how the two came apart.
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
