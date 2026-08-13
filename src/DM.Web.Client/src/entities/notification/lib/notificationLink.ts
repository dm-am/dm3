import {
  NotificationType,
  type UserNotification,
} from "@/shared/api/models/notifications";

/**
 * Where a notification points, or null when its payload carries no target.
 *
 * Lives beside the titles because the rule over them is a boundary, not a
 * habit: outside this slice the client does not name an event at all, and a
 * second table of words cannot be written without naming one.
 */
export function notificationLink(
  notification: UserNotification,
): string | null {
  const payload = notification.payload;
  if (!payload) return null;

  switch (notification.eventType) {
    case NotificationType.NewPublication:
    case NotificationType.LikedPublication:
    case NotificationType.NewBlogFromSubscribedAuthor:
      return payload.blogId ? `/blogs/${payload.blogId}` : null;

    case NotificationType.NewBlogComment:
    case NotificationType.NewPublicationComment:
    case NotificationType.LikedBlogComment:
      return payload.blogId ? `/blogs/${payload.blogId}` : null;

    case NotificationType.BlogInvitationCreated:
    case NotificationType.BlogInvitationAccepted:
    case NotificationType.BlogInvitationRejected:
      return payload.blogId ? `/blogs/${payload.blogId}` : null;

    case NotificationType.NewTopicInSubscribedBoard:
    case NotificationType.NewTopicFromSubscribedAuthor:
      return payload.topicId ? `/forum-topic/${payload.topicId}` : null;

    case NotificationType.NewCommentInSubscribedTopic:
      return payload.topicId ? `/forum-topic/${payload.topicId}` : null;

    case NotificationType.NewGameFromSubscribedAuthor:
    case NotificationType.NewGame:
      return payload.gameId ? `/game/${payload.gameId}` : null;

    case NotificationType.NewPostInSubscribedGame:
      return payload.gameId ? `/game/${payload.gameId}` : null;

    case NotificationType.NewCharacter:
      return payload.gameId ? `/game/${payload.gameId}/characters` : null;

    case NotificationType.LikedTopic:
    case NotificationType.NewTopic:
      return payload.topicId ? `/forum-topic/${payload.topicId}` : null;

    case NotificationType.NewTopicComment:
    case NotificationType.LikedTopicComment:
      return payload.topicId ? `/forum-topic/${payload.topicId}` : null;

    default:
      return null;
  }
}
