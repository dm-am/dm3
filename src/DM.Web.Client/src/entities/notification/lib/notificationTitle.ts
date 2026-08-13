import { NotificationType } from "@/shared/api/models/notifications";

/**
 * What a notification is called, in the words every channel already uses.
 *
 * The list kept a second table of these titles and it had drifted from the one
 * the dispatcher writes into letters and bot messages: one like on one topic
 * was "Лайк топика" in the list and "Лайк на топик" in the letter that followed
 * it minutes later. So the words live in the entity and are imported; a call
 * site that restates them is how that pair came about.
 *
 * The dispatcher holds the other half in NotificationText — neither tier can
 * import the other's file — and NotificationVocabularyShould in
 * DM.Architecture.Tests holds the two halves to the same words.
 *
 * The table is a subset of that one on purpose: an event is titled here only
 * where the letter about it carries a title too, and both fall back to the same
 * word where it does not. A title invented here would name an event that no
 * channel names, which is the pair this file exists to prevent.
 */
const TITLES: Partial<Record<NotificationType, string>> = {
  // Blog
  [NotificationType.NewPublication]: "Новая публикация",
  [NotificationType.LikedPublication]: "Лайк на публикацию",
  [NotificationType.NewBlogComment]: "Новый комментарий в блоге",
  [NotificationType.NewPublicationComment]: "Новый комментарий к публикации",
  [NotificationType.LikedBlogComment]: "Лайк на комментарий в блоге",
  [NotificationType.BlogInvitationCreated]: "Приглашение в блог",

  // Forum
  [NotificationType.NewTopic]: "Новый топик на форуме",
  [NotificationType.LikedTopic]: "Лайк на топик",
  [NotificationType.NewTopicComment]: "Новый комментарий в топике",
  [NotificationType.LikedTopicComment]: "Лайк на комментарий",

  // Games
  [NotificationType.NewCharacter]: "Новая заявка на персонажа",

  // Security. Untitled, all four of them showed as the bare fallback word in the
  // one list a reader checks after something happened to their account.
  [NotificationType.PasswordChanged]: "Пароль изменен",
  [NotificationType.EmailChanged]: "Почта изменена",
  [NotificationType.SuspiciousLoginActivity]: "Подозрительная активность входа",
  [NotificationType.AccountLocked]: "Вход в аккаунт временно заблокирован",

  // Subscriptions
  [NotificationType.NewCommentInSubscribedTopic]:
    "Новый комментарий в подписанном топике",
  [NotificationType.NewGameFromSubscribedAuthor]:
    "Новая игра от подписанного автора",
  [NotificationType.NewPostInSubscribedGame]: "Новый пост в подписанной игре",
  [NotificationType.NewTopicFromSubscribedAuthor]:
    "Новый топик от подписанного автора",
  [NotificationType.NewBlogFromSubscribedAuthor]:
    "Новый блог от подписанного автора",
};

/** Shown for an event that carries no title of its own. */
const UNKNOWN_TITLE = "Уведомление";

/** Title of a notification about the given event. */
export function notificationTitle(type: NotificationType): string {
  return TITLES[type] ?? UNKNOWN_TITLE;
}
