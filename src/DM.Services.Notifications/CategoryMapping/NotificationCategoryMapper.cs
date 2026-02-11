using DM.Services.Core.Dto.Enums;

namespace DM.Services.Notifications.CategoryMapping;

/// <summary>
/// Maps event types to notification categories for bot delivery filtering
/// </summary>
public static class NotificationCategoryMapper
{
    /// <summary>
    /// Get notification category for the given event type.
    /// Returns null if the event should not be delivered via bots.
    /// </summary>
    public static NotificationCategory? GetCategory(EventType eventType) => eventType switch
    {
        EventType.NewMessage or EventType.ChangedMessage or EventType.LikedMessage
            => NotificationCategory.Messages,

        EventType.NewForumTopic or EventType.ChangedForumTopic or EventType.LikedTopic or
        EventType.NewForumComment or EventType.ChangedForumComment or EventType.LikedForumComment
            => NotificationCategory.Forum,

        EventType.NewPublication or EventType.UpdatedPublication or EventType.LikedPublication or
        EventType.NewBlogComment or EventType.ChangedBlogComment or EventType.LikedBlogComment
            => NotificationCategory.Forum,

        EventType.StatusGameActive or EventType.StatusGameClosed or
        EventType.StatusGameFrozen or EventType.StatusGameFinished or
        EventType.NewCharacter or EventType.StatusCharacterAccepted or
        EventType.StatusCharacterDeclined or
        EventType.AssignmentRequestCreated or
        EventType.PlayerInvitationCreated or EventType.ReaderInvitationCreated or
        EventType.PostReviewed
            => NotificationCategory.Games,

        EventType.NewTopicInSubscribedBoard or EventType.NewCommentInSubscribedTopic or
        EventType.NewGameFromSubscribedAuthor or EventType.NewPostInSubscribedGame
            => NotificationCategory.Subscriptions,

        EventType.PasswordChanged or EventType.EmailChanged or
        EventType.SuspiciousLoginActivity
            => NotificationCategory.Security,

        EventType.TicketCreated or EventType.TicketAssigned or EventType.TicketResolved or
        EventType.WarningIssued or EventType.BanIssued or EventType.BanLifted
            => NotificationCategory.Moderation,

        _ => null
    };
}
