using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Features.Notifications;

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

        EventType.NewTopic or EventType.ChangedTopic or EventType.LikedTopic or
        EventType.NewTopicComment or EventType.ChangedTopicComment or EventType.LikedTopicComment
            => NotificationCategory.Forum,

        EventType.NewPublication or EventType.ChangedPublication or EventType.LikedPublication or
        EventType.NewBlogComment or EventType.ChangedBlogComment or EventType.LikedBlogComment or
        EventType.NewPublicationComment or EventType.ChangedPublicationComment or EventType.LikedPublicationComment or
        EventType.BlogInvitationCreated or EventType.BlogInvitationAccepted or EventType.BlogInvitationRejected
            => NotificationCategory.Blog,

        EventType.StatusGameActive or EventType.StatusGameClosed or
        EventType.StatusGameFrozen or EventType.StatusGameFinished or
        EventType.GameClosureWarning or EventType.GameRecruitmentOpened or
        EventType.NewCharacter or EventType.StatusCharacterAccepted or
        EventType.StatusCharacterDeclined or EventType.StatusCharacterExiled or
        EventType.StatusCharacterRetired or EventType.StatusCharacterDied or
        EventType.StatusCharacterResurrected or EventType.StatusCharacterLeft or
        EventType.StatusCharacterReturned or
        EventType.AssignmentRequestCreated or
        EventType.PlayerInvitationCreated or EventType.ReaderInvitationCreated or
        EventType.RoomPendencyCreated or
        EventType.PostReviewed
            => NotificationCategory.Games,

        EventType.NewCommentInSubscribedTopic or
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
