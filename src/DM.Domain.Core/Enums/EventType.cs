using DM.Domain.Core.Extensions;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Message queue event type, numbered by ranges named in the section headers below
/// </summary>
public enum EventType
{
    Unknown = 0,

    [EventRoutingKey("community.user.activated")]
    ActivatedUser = 2,

    [EventRoutingKey("messaging.message.created")]
    NewMessage = 11,

    [EventRoutingKey("messaging.message.changed")]
    ChangedMessage = 12,

    [EventRoutingKey("messaging.message.liked")]
    LikedMessage = 13,

    [EventRoutingKey("messaging.message.deleted")]
    DeletedMessage = 14,

    /// <summary>
    /// New message has been sent to the global chat.
    /// Emitted in addition to <see cref="NewMessage"/> so realtime
    /// consumers can broadcast public chat traffic to every connected
    /// client without inspecting the chat type.
    /// </summary>
    [EventRoutingKey("messaging.message.global.created")]
    NewGlobalChatMessage = 15,

    /// <summary>
    /// User avatar has been changed (upload or reset).
    /// Transient UI hint — payload includes user ID and new
    /// picture URLs so that open tabs can live-update
    /// avatars in chats/comments without a reload.
    /// </summary>
    [EventRoutingKey("community.user.avatar.changed")]
    UserAvatarChanged = 24,

    [EventRoutingKey("community.poll.created")]
    NewPoll = 31,

    [EventRoutingKey("community.testimonial.website.created")]
    NewWebsiteTestimonial = 32,

    [EventRoutingKey("blog.publication.created")]
    NewPublication = 41,

    [EventRoutingKey("blog.publication.changed")]
    ChangedPublication = 42,

    [EventRoutingKey("blog.publication.deleted")]
    DeletedPublication = 43,

    [EventRoutingKey("blog.publication.liked")]
    LikedPublication = 44,

    [EventRoutingKey("blog.comment.created")]
    NewBlogComment = 45,

    [EventRoutingKey("blog.comment.changed")]
    ChangedBlogComment = 46,

    [EventRoutingKey("blog.comment.deleted")]
    DeletedBlogComment = 47,

    [EventRoutingKey("blog.comment.liked")]
    LikedBlogComment = 48,

    [EventRoutingKey("blog.publication.comment.created")]
    NewPublicationComment = 49,

    [EventRoutingKey("blog.publication.comment.changed")]
    ChangedPublicationComment = 50,

    [EventRoutingKey("blog.publication.comment.deleted")]
    DeletedPublicationComment = 51,

    [EventRoutingKey("blog.publication.comment.liked")]
    LikedPublicationComment = 52,

    [EventRoutingKey("blog.invitation.created")]
    BlogInvitationCreated = 53,

    [EventRoutingKey("blog.invitation.accepted")]
    BlogInvitationAccepted = 54,

    [EventRoutingKey("blog.invitation.rejected")]
    BlogInvitationRejected = 55,

    [EventRoutingKey("blog.invitation.cancelled")]
    BlogInvitationCancelled = 56,

    [EventRoutingKey("chat.event.started")]
    GlobalChatEventStarted = 61,

    [EventRoutingKey("chat.event.ended")]
    GlobalChatEventEnded = 62,

    [EventRoutingKey("chat.event.participant.joined")]
    GlobalChatEventParticipantJoined = 63,

    [EventRoutingKey("chat.event.participant.left")]
    GlobalChatEventParticipantLeft = 64,

    [EventRoutingKey("subscription.topic.comment.created")]
    NewCommentInSubscribedTopic = 72,

    /// <summary>
    /// New game from a master user is subscribed to
    /// </summary>
    [EventRoutingKey("subscription.author.game.created")]
    NewGameFromSubscribedAuthor = 73,

    /// <summary>
    /// New post in a game user is subscribed to (as reader)
    /// </summary>
    [EventRoutingKey("subscription.game.post.created")]
    NewPostInSubscribedGame = 74,

    [EventRoutingKey("subscription.author.topic.created")]
    NewTopicFromSubscribedAuthor = 77,

    /// <summary>
    /// New blog from an author user is subscribed to —
    /// fired when the blog is created with public-draft visibility or
    /// when it transitions to Active status.
    /// </summary>
    [EventRoutingKey("subscription.author.blog.created")]
    NewBlogFromSubscribedAuthor = 78,

    [EventRoutingKey("moderation.ticket.created")]
    TicketCreated = 81,

    /// <summary>
    /// A moderator has written in the conversation of a ticket. The entity is the
    /// comment.
    /// </summary>
    [EventRoutingKey("moderation.ticket.answered")]
    TicketAnswered = 82,

    [EventRoutingKey("moderation.ticket.resolved")]
    TicketResolved = 83,

    [EventRoutingKey("moderation.warning.issued")]
    WarningIssued = 84,

    [EventRoutingKey("moderation.ban.issued")]
    BanIssued = 85,

    [EventRoutingKey("moderation.ban.lifted")]
    BanLifted = 86,

    /// <summary>
    /// User has been granted an award by moderator/admin
    /// </summary>
    [EventRoutingKey("community.award.granted")]
    AwardGranted = 87,

    /// <remarks>
    /// A warning is never withdrawn, so this is the only change it can announce.
    /// </remarks>
    [EventRoutingKey("moderation.warning.points-changed")]
    WarningPointsChanged = 88,

    /// <summary>
    /// The author of a ticket has written in its conversation, from his account or
    /// as the guest holding its tracking token. The entity is the comment.
    /// </summary>
    [EventRoutingKey("moderation.ticket.replied")]
    TicketReplied = 89,

    [EventRoutingKey("security.password.changed")]
    PasswordChanged = 91,

    [EventRoutingKey("security.email.changed")]
    EmailChanged = 92,

    [EventRoutingKey("security.session.terminated")]
    SessionTerminated = 94,

    /// <summary>
    /// Account was locked due to failed login attempts
    /// </summary>
    [EventRoutingKey("security.account.locked")]
    AccountLocked = 95,

    [EventRoutingKey("security.login.suspicious")]
    SuspiciousLoginActivity = 96,

    /// <summary>
    /// User account has been deactivated (soft deleted)
    /// </summary>
    [EventRoutingKey("security.account.deactivated")]
    AccountDeactivated = 97,

    [EventRoutingKey("forum.topic.created")]
    NewTopic = 101,

    [EventRoutingKey("forum.topic.changed")]
    ChangedTopic = 102,

    [EventRoutingKey("forum.topic.deleted")]
    DeletedTopic = 103,

    [EventRoutingKey("forum.topic.liked")]
    LikedTopic = 104,

    [EventRoutingKey("forum.topic.comment.created")]
    NewTopicComment = 111,

    [EventRoutingKey("forum.topic.comment.changed")]
    ChangedTopicComment = 112,

    [EventRoutingKey("forum.topic.comment.deleted")]
    DeletedTopicComment = 113,

    [EventRoutingKey("forum.topic.comment.liked")]
    LikedTopicComment = 114,

    // Blog publications and comments stay in the 41-59 block — these slots are reserved for the
    // blog ENTITY itself.

    [EventRoutingKey("blog.created")]
    NewBlog = 201,

    [EventRoutingKey("blog.changed")]
    ChangedBlog = 202,

    [EventRoutingKey("blog.status.moderation")]
    StatusBlogModeration = 221,

    [EventRoutingKey("blog.status.active")]
    StatusBlogActive = 224,

    [EventRoutingKey("blog.status.frozen")]
    StatusBlogFrozen = 225,

    [EventRoutingKey("blog.status.finished")]
    StatusBlogFinished = 226,

    [EventRoutingKey("blog.status.closed")]
    StatusBlogClosed = 227,

    [EventRoutingKey("game.created")]
    NewGame = 301,

    [EventRoutingKey("game.changed")]
    ChangedGame = 302,

    [EventRoutingKey("game.deleted")]
    DeletedGame = 303,

    [EventRoutingKey("game.status.moderation")]
    StatusGameModeration = 321,

    /// <summary>
    /// Game has started
    /// </summary>
    [EventRoutingKey("game.status.active")]
    StatusGameActive = 324,

    [EventRoutingKey("game.status.frozen")]
    StatusGameFrozen = 325,

    [EventRoutingKey("game.status.finished")]
    StatusGameFinished = 326,

    [EventRoutingKey("game.status.closed")]
    StatusGameClosed = 327,

    /// <summary>
    /// Game closure warning - game will be closed due to inactivity
    /// </summary>
    [EventRoutingKey("game.closure.warning")]
    GameClosureWarning = 328,

    [EventRoutingKey("game.recruitment.opened")]
    GameRecruitmentOpened = 329,

    /// <summary>
    /// Game inactivity warning - game will be frozen due to no posts for 1 month
    /// </summary>
    [EventRoutingKey("game.inactivity.warning")]
    GameInactivityWarning = 330,

    [EventRoutingKey("game.comment.created")]
    NewGameComment = 331,

    [EventRoutingKey("game.comment.changed")]
    ChangedGameComment = 332,

    [EventRoutingKey("game.comment.deleted")]
    DeletedGameComment = 333,

    [EventRoutingKey("game.comment.liked")]
    LikedGameComment = 334,

    /// <summary>
    /// Assistant assignment request created and pending
    /// </summary>
    [EventRoutingKey("game.assignment.created")]
    AssignmentRequestCreated = 351,

    /// <summary>
    /// Assistant assignment request has been accepted
    /// </summary>
    [EventRoutingKey("game.assignment.accepted")]
    AssignmentRequestAccepted = 352,

    /// <summary>
    /// Assistant assignment request has been rejected
    /// </summary>
    [EventRoutingKey("game.assignment.rejected")]
    AssignmentRequestRejected = 353,

    [EventRoutingKey("game.invitation.player.created")]
    PlayerInvitationCreated = 354,

    [EventRoutingKey("game.invitation.player.accepted")]
    PlayerInvitationAccepted = 355,

    [EventRoutingKey("game.invitation.player.rejected")]
    PlayerInvitationRejected = 356,

    [EventRoutingKey("game.invitation.reader.created")]
    ReaderInvitationCreated = 357,

    [EventRoutingKey("game.invitation.reader.accepted")]
    ReaderInvitationAccepted = 358,

    [EventRoutingKey("game.invitation.reader.rejected")]
    ReaderInvitationRejected = 359,

    /// <summary>
    /// Assistant assignment request has been cancelled
    /// </summary>
    [EventRoutingKey("game.assignment.cancelled")]
    AssignmentRequestCancelled = 364,

    [EventRoutingKey("game.invitation.player.cancelled")]
    PlayerInvitationCancelled = 365,

    [EventRoutingKey("game.invitation.reader.cancelled")]
    ReaderInvitationCancelled = 366,

    [EventRoutingKey("game.character.created")]
    NewCharacter = 361,

    [EventRoutingKey("game.character.changed")]
    ChangedCharacter = 362,

    [EventRoutingKey("game.character.deleted")]
    DeletedCharacter = 363,

    [EventRoutingKey("game.character.status.declined")]
    StatusCharacterDeclined = 371,

    [EventRoutingKey("game.character.status.accepted")]
    StatusCharacterAccepted = 372,

    [EventRoutingKey("game.character.status.died")]
    StatusCharacterDied = 373,

    [EventRoutingKey("game.character.status.resurrected")]
    StatusCharacterResurrected = 374,

    [EventRoutingKey("game.character.status.left")]
    StatusCharacterLeft = 375,

    /// <summary>
    /// Character has been exiled from the game by GM
    /// </summary>
    [EventRoutingKey("game.character.status.exiled")]
    StatusCharacterExiled = 377,

    [EventRoutingKey("game.character.status.retired")]
    StatusCharacterRetired = 378,

    /// <summary>
    /// Exiled character has been brought back into the game by GM
    /// </summary>
    [EventRoutingKey("game.character.status.readmitted")]
    StatusCharacterReadmitted = 379,

    [EventRoutingKey("game.room.created")]
    NewRoom = 381,

    [EventRoutingKey("game.room.changed")]
    ChangedRoom = 382,

    [EventRoutingKey("game.room.deleted")]
    DeletedRoom = 383,

    /// <summary>
    /// New post pendency has been created
    /// </summary>
    [EventRoutingKey("game.room.pendency.created")]
    RoomPendencyCreated = 384,

    /// <summary>
    /// Post pendency has been fulfilled
    /// </summary>
    [EventRoutingKey("game.room.pendency.fulfilled")]
    RoomPendencyFulfilled = 385,

    [EventRoutingKey("game.room.pendency.deleted")]
    RoomPendencyDeleted = 386,

    /// <summary>
    /// Reminder for unfulfilled post pendency (sent periodically)
    /// </summary>
    [EventRoutingKey("game.room.pendency.reminder")]
    RoomPendencyReminder = 387,

    [EventRoutingKey("game.post.created")]
    NewPost = 401,

    [EventRoutingKey("game.post.changed")]
    ChangedPost = 402,

    [EventRoutingKey("game.post.deleted")]
    DeletedPost = 403,

    /// <summary>
    /// Post has been reviewed (rated)
    /// </summary>
    [EventRoutingKey("game.post.reviewed")]
    PostReviewed = 411,
}
