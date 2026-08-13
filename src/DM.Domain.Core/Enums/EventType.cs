using DM.Domain.Core.Extensions;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Message queue event type
///
/// 1-29    Community events (User, Message, User status)
/// 31-39   Poll events
/// 41-59   Blog events (Publication, BlogComment, PublicationComment, Invitation)
/// 61-69   GlobalChatEvent
/// 71-79   Subscription events
/// 81-89   Moderation events
/// 91-99   Security events
/// 101-119 Forum events (Topic, TopicComment)
/// 201-249 Blog lifecycle events (Blog entity)
/// 301-499 Game events
/// </summary>
public enum EventType
{
    /// <summary>
    /// Unknown event
    /// </summary>
    Unknown = 0,

    // ========================================
    // User events (1-9)
    // ========================================

    /// <summary>
    /// New user has been created
    /// </summary>
    [EventRoutingKey("community.user.registered")]
    NewUser = 1,

    /// <summary>
    /// User has been activated
    /// </summary>
    [EventRoutingKey("community.user.activated")]
    ActivatedUser = 2,

    // ========================================
    // Message events (11-19)
    // ========================================

    /// <summary>
    /// New message has been sent
    /// </summary>
    [EventRoutingKey("messaging.message.created")]
    NewMessage = 11,

    /// <summary>
    /// Message has been changed
    /// </summary>
    [EventRoutingKey("messaging.message.changed")]
    ChangedMessage = 12,

    /// <summary>
    /// Message has been liked
    /// </summary>
    [EventRoutingKey("messaging.message.liked")]
    LikedMessage = 13,

    /// <summary>
    /// Message has been deleted
    /// </summary>
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

    // ========================================
    // User status events (21-29)
    // ========================================

    /// <summary>
    /// User role has been changed by administration
    /// </summary>
    [EventRoutingKey("community.user.role.changed")]
    RoleChanged = 21,

    /// <summary>
    /// User is no longer a newbie (passed probation period)
    /// </summary>
    [EventRoutingKey("community.user.newbie.graduated")]
    NoLongerNewbie = 23,

    /// <summary>
    /// User avatar has been changed (upload or reset).
    /// Transient UI hint — payload includes user ID and new
    /// picture URLs so that open tabs can live-update
    /// avatars in chats/comments without a reload.
    /// </summary>
    [EventRoutingKey("community.user.avatar.changed")]
    UserAvatarChanged = 24,

    // ========================================
    // Poll events (31-39)
    // ========================================

    /// <summary>
    /// New poll has been published
    /// </summary>
    [EventRoutingKey("community.poll.created")]
    NewPoll = 31,

    /// <summary>
    /// New website testimonial has been created
    /// </summary>
    [EventRoutingKey("community.testimonial.website.created")]
    NewWebsiteTestimonial = 32,

    /// <summary>
    /// Review has been liked
    /// </summary>
    [EventRoutingKey("community.review.liked")]
    LikedReview = 35,

    // ========================================
    // Blog events (41-59)
    // ========================================

    /// <summary>
    /// New blog publication has been created
    /// </summary>
    [EventRoutingKey("blog.publication.created")]
    NewPublication = 41,

    /// <summary>
    /// Blog publication has been changed
    /// </summary>
    [EventRoutingKey("blog.publication.changed")]
    ChangedPublication = 42,

    /// <summary>
    /// Blog publication has been deleted
    /// </summary>
    [EventRoutingKey("blog.publication.deleted")]
    DeletedPublication = 43,

    /// <summary>
    /// Blog publication has been liked
    /// </summary>
    [EventRoutingKey("blog.publication.liked")]
    LikedPublication = 44,

    /// <summary>
    /// New blog comment has been created
    /// </summary>
    [EventRoutingKey("blog.comment.created")]
    NewBlogComment = 45,

    /// <summary>
    /// Blog comment has been changed
    /// </summary>
    [EventRoutingKey("blog.comment.changed")]
    ChangedBlogComment = 46,

    /// <summary>
    /// Blog comment has been deleted
    /// </summary>
    [EventRoutingKey("blog.comment.deleted")]
    DeletedBlogComment = 47,

    /// <summary>
    /// Blog comment has been liked
    /// </summary>
    [EventRoutingKey("blog.comment.liked")]
    LikedBlogComment = 48,

    /// <summary>
    /// New publication comment has been created
    /// </summary>
    [EventRoutingKey("blog.publication.comment.created")]
    NewPublicationComment = 49,

    /// <summary>
    /// Publication comment has been changed
    /// </summary>
    [EventRoutingKey("blog.publication.comment.changed")]
    ChangedPublicationComment = 50,

    /// <summary>
    /// Publication comment has been deleted
    /// </summary>
    [EventRoutingKey("blog.publication.comment.deleted")]
    DeletedPublicationComment = 51,

    /// <summary>
    /// Publication comment has been liked
    /// </summary>
    [EventRoutingKey("blog.publication.comment.liked")]
    LikedPublicationComment = 52,

    /// <summary>
    /// Blog invitation has been created
    /// </summary>
    [EventRoutingKey("blog.invitation.created")]
    BlogInvitationCreated = 53,

    /// <summary>
    /// Blog invitation has been accepted
    /// </summary>
    [EventRoutingKey("blog.invitation.accepted")]
    BlogInvitationAccepted = 54,

    /// <summary>
    /// Blog invitation has been rejected
    /// </summary>
    [EventRoutingKey("blog.invitation.rejected")]
    BlogInvitationRejected = 55,

    /// <summary>
    /// Blog invitation has been cancelled
    /// </summary>
    [EventRoutingKey("blog.invitation.cancelled")]
    BlogInvitationCancelled = 56,

    // ========================================
    // GlobalChatEvent events (61-69)
    // ========================================

    /// <summary>
    /// Global chat event has been started
    /// </summary>
    [EventRoutingKey("chat.event.started")]
    GlobalChatEventStarted = 61,

    /// <summary>
    /// Global chat event has been ended
    /// </summary>
    [EventRoutingKey("chat.event.ended")]
    GlobalChatEventEnded = 62,

    /// <summary>
    /// User joined a global chat event
    /// </summary>
    [EventRoutingKey("chat.event.participant.joined")]
    GlobalChatEventParticipantJoined = 63,

    /// <summary>
    /// User left a global chat event
    /// </summary>
    [EventRoutingKey("chat.event.participant.left")]
    GlobalChatEventParticipantLeft = 64,

    // ========================================
    // Subscription notification events (71-79)
    // ========================================

    /// <summary>
    /// New topic in a board user is subscribed to
    /// </summary>
    [EventRoutingKey("subscription.board.topic.created")]
    NewTopicInSubscribedBoard = 71,

    /// <summary>
    /// New comment in a topic user is subscribed to
    /// </summary>
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

    /// <summary>
    /// User was mentioned in a comment or message
    /// </summary>
    [EventRoutingKey("community.mention.created")]
    UserMentioned = 75,

    // Slot 76 was NewPublicationFromSubscribedAuthor — removed; per-publication
    // notifications are no longer part of the user-subscription contract.
    // Subscribers now receive a single NewBlogFromSubscribedAuthor signal
    // when the blog itself becomes visible (public draft creation OR
    // activation), not on every internal publication.

    /// <summary>
    /// New forum topic from an author user is subscribed to
    /// </summary>
    [EventRoutingKey("subscription.author.topic.created")]
    NewTopicFromSubscribedAuthor = 77,

    /// <summary>
    /// New blog from an author user is subscribed to —
    /// fired when the blog is created with public-draft visibility or
    /// when it transitions to Active status.
    /// </summary>
    [EventRoutingKey("subscription.author.blog.created")]
    NewBlogFromSubscribedAuthor = 78,

    // ========================================
    // Moderation events (81-89)
    // ========================================

    /// <summary>
    /// New moderation ticket has been created
    /// </summary>
    [EventRoutingKey("moderation.ticket.created")]
    TicketCreated = 81,

    /// <summary>
    /// Moderation ticket has been assigned to moderator
    /// </summary>
    [EventRoutingKey("moderation.ticket.assigned")]
    TicketAssigned = 82,

    /// <summary>
    /// Moderation ticket has been resolved
    /// </summary>
    [EventRoutingKey("moderation.ticket.resolved")]
    TicketResolved = 83,

    /// <summary>
    /// User has received a warning
    /// </summary>
    [EventRoutingKey("moderation.warning.issued")]
    WarningIssued = 84,

    /// <summary>
    /// User has been banned
    /// </summary>
    [EventRoutingKey("moderation.ban.issued")]
    BanIssued = 85,

    /// <summary>
    /// User ban has been lifted
    /// </summary>
    [EventRoutingKey("moderation.ban.lifted")]
    BanLifted = 86,

    /// <summary>
    /// User has been granted an award by moderator/admin
    /// </summary>
    [EventRoutingKey("community.award.granted")]
    AwardGranted = 87,

    // ========================================
    // Security audit events (91-99)
    // ========================================

    /// <summary>
    /// User password has been changed
    /// </summary>
    [EventRoutingKey("security.password.changed")]
    PasswordChanged = 91,

    /// <summary>
    /// User email has been changed
    /// </summary>
    [EventRoutingKey("security.email.changed")]
    EmailChanged = 92,

    /// <summary>
    /// Password reset was requested
    /// </summary>
    [EventRoutingKey("security.password.reset.requested")]
    PasswordResetRequested = 93,

    /// <summary>
    /// User session was terminated
    /// </summary>
    [EventRoutingKey("security.session.terminated")]
    SessionTerminated = 94,

    /// <summary>
    /// Account was locked due to failed login attempts
    /// </summary>
    [EventRoutingKey("security.account.locked")]
    AccountLocked = 95,

    /// <summary>
    /// Suspicious login activity detected
    /// </summary>
    [EventRoutingKey("security.login.suspicious")]
    SuspiciousLoginActivity = 96,

    /// <summary>
    /// User account has been deactivated (soft deleted)
    /// </summary>
    [EventRoutingKey("security.account.deactivated")]
    AccountDeactivated = 97,

    // ========================================
    // Forum events (101-119)
    // ========================================

    /// <summary>
    /// New topic has been created
    /// </summary>
    [EventRoutingKey("forum.topic.created")]
    NewTopic = 101,

    /// <summary>
    /// Topic has been changed
    /// </summary>
    [EventRoutingKey("forum.topic.changed")]
    ChangedTopic = 102,

    /// <summary>
    /// Topic has been deleted
    /// </summary>
    [EventRoutingKey("forum.topic.deleted")]
    DeletedTopic = 103,

    /// <summary>
    /// Topic has been liked
    /// </summary>
    [EventRoutingKey("forum.topic.liked")]
    LikedTopic = 104,

    /// <summary>
    /// New topic comment has been created
    /// </summary>
    [EventRoutingKey("forum.topic.comment.created")]
    NewTopicComment = 111,

    /// <summary>
    /// Topic comment has been changed
    /// </summary>
    [EventRoutingKey("forum.topic.comment.changed")]
    ChangedTopicComment = 112,

    /// <summary>
    /// Topic comment has been deleted
    /// </summary>
    [EventRoutingKey("forum.topic.comment.deleted")]
    DeletedTopicComment = 113,

    /// <summary>
    /// Topic comment has been liked
    /// </summary>
    [EventRoutingKey("forum.topic.comment.liked")]
    LikedTopicComment = 114,

    // ========================================
    // Blog lifecycle events (201-249)
    // ========================================
    // Mirrors the Game lifecycle (301-329) one-to-one. Blog publications
    // and comments stay in the 41-59 block — these slots are reserved for
    // the blog ENTITY itself.

    /// <summary>
    /// New blog has been created.
    /// </summary>
    [EventRoutingKey("blog.created")]
    NewBlog = 201,

    /// <summary>
    /// Blog has been changed.
    /// </summary>
    [EventRoutingKey("blog.changed")]
    ChangedBlog = 202,

    /// <summary>
    /// Blog is on moderation.
    /// </summary>
    [EventRoutingKey("blog.status.moderation")]
    StatusBlogModeration = 221,

    /// <summary>
    /// Blog has transitioned to Active status.
    /// </summary>
    [EventRoutingKey("blog.status.active")]
    StatusBlogActive = 224,

    /// <summary>
    /// Blog is frozen.
    /// </summary>
    [EventRoutingKey("blog.status.frozen")]
    StatusBlogFrozen = 225,

    /// <summary>
    /// Blog is finished.
    /// </summary>
    [EventRoutingKey("blog.status.finished")]
    StatusBlogFinished = 226,

    /// <summary>
    /// Blog is closed.
    /// </summary>
    [EventRoutingKey("blog.status.closed")]
    StatusBlogClosed = 227,

    // ========================================
    // Game events (301-499)
    // ========================================

    /// <summary>
    /// New game has been created
    /// </summary>
    [EventRoutingKey("game.created")]
    NewGame = 301,

    /// <summary>
    /// Game has been changed
    /// </summary>
    [EventRoutingKey("game.changed")]
    ChangedGame = 302,

    /// <summary>
    /// Game has been deleted
    /// </summary>
    [EventRoutingKey("game.deleted")]
    DeletedGame = 303,

    /// <summary>
    /// Game is on moderation
    /// </summary>
    [EventRoutingKey("game.status.moderation")]
    StatusGameModeration = 321,

    /// <summary>
    /// Game is draft
    /// </summary>
    [EventRoutingKey("game.status.draft")]
    StatusGameDraft = 322,

    /// <summary>
    /// Game is released
    /// </summary>
    [EventRoutingKey("game.status.requirement")]
    StatusGameRequirement = 323,

    /// <summary>
    /// Game has started
    /// </summary>
    [EventRoutingKey("game.status.active")]
    StatusGameActive = 324,

    /// <summary>
    /// Game is frozen
    /// </summary>
    [EventRoutingKey("game.status.frozen")]
    StatusGameFrozen = 325,

    /// <summary>
    /// Game is finished
    /// </summary>
    [EventRoutingKey("game.status.finished")]
    StatusGameFinished = 326,

    /// <summary>
    /// Game is closed
    /// </summary>
    [EventRoutingKey("game.status.closed")]
    StatusGameClosed = 327,

    /// <summary>
    /// Game closure warning - game will be closed due to inactivity
    /// </summary>
    [EventRoutingKey("game.closure.warning")]
    GameClosureWarning = 328,

    /// <summary>
    /// Game recruitment has been opened
    /// </summary>
    [EventRoutingKey("game.recruitment.opened")]
    GameRecruitmentOpened = 329,

    /// <summary>
    /// Game inactivity warning - game will be frozen due to no posts for 1 month
    /// </summary>
    [EventRoutingKey("game.inactivity.warning")]
    GameInactivityWarning = 330,

    /// <summary>
    /// New game comment has been created
    /// </summary>
    [EventRoutingKey("game.comment.created")]
    NewGameComment = 331,

    /// <summary>
    /// Game comment has been changed
    /// </summary>
    [EventRoutingKey("game.comment.changed")]
    ChangedGameComment = 332,

    /// <summary>
    /// Game comment has been deleted
    /// </summary>
    [EventRoutingKey("game.comment.deleted")]
    DeletedGameComment = 333,

    /// <summary>
    /// Game comment has been liked
    /// </summary>
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

    /// <summary>
    /// Player invitation has been created
    /// </summary>
    [EventRoutingKey("game.invitation.player.created")]
    PlayerInvitationCreated = 354,

    /// <summary>
    /// Player invitation has been accepted
    /// </summary>
    [EventRoutingKey("game.invitation.player.accepted")]
    PlayerInvitationAccepted = 355,

    /// <summary>
    /// Player invitation has been rejected
    /// </summary>
    [EventRoutingKey("game.invitation.player.rejected")]
    PlayerInvitationRejected = 356,

    /// <summary>
    /// Reader invitation has been created
    /// </summary>
    [EventRoutingKey("game.invitation.reader.created")]
    ReaderInvitationCreated = 357,

    /// <summary>
    /// Reader invitation has been accepted
    /// </summary>
    [EventRoutingKey("game.invitation.reader.accepted")]
    ReaderInvitationAccepted = 358,

    /// <summary>
    /// Reader invitation has been rejected
    /// </summary>
    [EventRoutingKey("game.invitation.reader.rejected")]
    ReaderInvitationRejected = 359,

    /// <summary>
    /// Assistant assignment request has been cancelled
    /// </summary>
    [EventRoutingKey("game.assignment.cancelled")]
    AssignmentRequestCancelled = 364,

    /// <summary>
    /// Player invitation has been cancelled
    /// </summary>
    [EventRoutingKey("game.invitation.player.cancelled")]
    PlayerInvitationCancelled = 365,

    /// <summary>
    /// Reader invitation has been cancelled
    /// </summary>
    [EventRoutingKey("game.invitation.reader.cancelled")]
    ReaderInvitationCancelled = 366,

    /// <summary>
    /// New character has been created
    /// </summary>
    [EventRoutingKey("game.character.created")]
    NewCharacter = 361,

    /// <summary>
    /// Character has been changed
    /// </summary>
    [EventRoutingKey("game.character.changed")]
    ChangedCharacter = 362,

    /// <summary>
    /// Character has been deleted
    /// </summary>
    [EventRoutingKey("game.character.deleted")]
    DeletedCharacter = 363,

    /// <summary>
    /// Character has been declined
    /// </summary>
    [EventRoutingKey("game.character.status.declined")]
    StatusCharacterDeclined = 371,

    /// <summary>
    /// Character has been accepted
    /// </summary>
    [EventRoutingKey("game.character.status.accepted")]
    StatusCharacterAccepted = 372,

    /// <summary>
    /// Character has been killed
    /// </summary>
    [EventRoutingKey("game.character.status.died")]
    StatusCharacterDied = 373,

    /// <summary>
    /// Character has been resurrected
    /// </summary>
    [EventRoutingKey("game.character.status.resurrected")]
    StatusCharacterResurrected = 374,

    /// <summary>
    /// Character has left the game
    /// </summary>
    [EventRoutingKey("game.character.status.left")]
    StatusCharacterLeft = 375,

    /// <summary>
    /// Character has returned to the game
    /// </summary>
    [EventRoutingKey("game.character.status.returned")]
    StatusCharacterReturned = 376,

    /// <summary>
    /// Character has been exiled from the game by GM
    /// </summary>
    [EventRoutingKey("game.character.status.exiled")]
    StatusCharacterExiled = 377,

    /// <summary>
    /// Character has retired from the game
    /// </summary>
    [EventRoutingKey("game.character.status.retired")]
    StatusCharacterRetired = 378,

    /// <summary>
    /// New room has been created
    /// </summary>
    [EventRoutingKey("game.room.created")]
    NewRoom = 381,

    /// <summary>
    /// Room has been changed
    /// </summary>
    [EventRoutingKey("game.room.changed")]
    ChangedRoom = 382,

    /// <summary>
    /// Room has been deleted
    /// </summary>
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

    /// <summary>
    /// Post pendency has been deleted
    /// </summary>
    [EventRoutingKey("game.room.pendency.deleted")]
    RoomPendencyDeleted = 386,

    /// <summary>
    /// Reminder for unfulfilled post pendency (sent periodically)
    /// </summary>
    [EventRoutingKey("game.room.pendency.reminder")]
    RoomPendencyReminder = 387,

    /// <summary>
    /// New game post has been created
    /// </summary>
    [EventRoutingKey("game.post.created")]
    NewPost = 401,

    /// <summary>
    /// Post has been changed
    /// </summary>
    [EventRoutingKey("game.post.changed")]
    ChangedPost = 402,

    /// <summary>
    /// Post has been deleted
    /// </summary>
    [EventRoutingKey("game.post.deleted")]
    DeletedPost = 403,

    /// <summary>
    /// Post has been reviewed (rated)
    /// </summary>
    [EventRoutingKey("game.post.reviewed")]
    PostReviewed = 411,
}
