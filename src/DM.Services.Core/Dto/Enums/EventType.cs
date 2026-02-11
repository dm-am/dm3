using DM.Services.Core.Extensions;

namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Message queue event type
///
/// 1-99    Community events
/// 100-199 Forum events
/// 200-299 TBD
/// 300-499 Game events
/// </summary>
public enum EventType
{
    /// <summary>
    /// Unknown event
    /// </summary>
    Unknown = 0,

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
    /// New chat message has been sent
    /// </summary>
    [EventRoutingKey("chat.message.created")]
    NewChatMessage = 31,

    /// <summary>
    /// Chat message has been liked
    /// </summary>
    [EventRoutingKey("chat.message.liked")]
    LikedChatMessage = 32,

    // ========================================
    // User status events (21-29)
    // ========================================

    /// <summary>
    /// User role has been changed by administration
    /// </summary>
    [EventRoutingKey("community.user.role.changed")]
    RoleChanged = 21,

    /// <summary>
    /// User has been granted honorary status
    /// </summary>
    [EventRoutingKey("community.user.honorary.granted")]
    HonoraryGranted = 22,

    /// <summary>
    /// User is no longer a newbie (passed probation period)
    /// </summary>
    [EventRoutingKey("community.user.newbie.graduated")]
    NoLongerNewbie = 23,

    // ========================================
    // Poll events (51-59)
    // ========================================

    /// <summary>
    /// New poll has been published
    /// </summary>
    [EventRoutingKey("community.poll.created")]
    NewPoll = 51,

    // ========================================
    // Blog events (41-50)
    // ========================================

    /// <summary>
    /// New blog publication has been created
    /// </summary>
    [EventRoutingKey("blog.publication.created")]
    NewPublication = 41,

    /// <summary>
    /// Blog publication has been updated
    /// </summary>
    [EventRoutingKey("blog.publication.updated")]
    UpdatedPublication = 42,

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
    /// Blog comment has been updated
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

    // ========================================
    // Moderation events (81-99)
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

    // ========================================
    // Forum events (101-199)
    // ========================================

    /// <summary>
    /// New topic has been created
    /// </summary>
    [EventRoutingKey("forum.topic.created")]
    NewForumTopic = 101,

    /// <summary>
    /// Topic text has been updated
    /// </summary>
    [EventRoutingKey("forum.topic.changed")]
    ChangedForumTopic = 102,

    /// <summary>
    /// Topic has been deleted
    /// </summary>
    [EventRoutingKey("forum.topic.deleted")]
    DeletedForumTopic = 103,

    /// <summary>
    /// Topic has been liked
    /// </summary>
    [EventRoutingKey("forum.topic.liked")]
    LikedTopic = 104,

    /// <summary>
    /// New commentary has been created on forum
    /// </summary>
    [EventRoutingKey("forum.comment.created")]
    NewForumComment = 111,

    /// <summary>
    /// Forum commentary has been updated
    /// </summary>
    [EventRoutingKey("forum.comment.changed")]
    ChangedForumComment = 112,

    /// <summary>
    /// Forum commentary has been deleted
    /// </summary>
    [EventRoutingKey("forum.comment.deleted")]
    DeletedForumComment = 113,

    /// <summary>
    /// Forum commentary has been liked
    /// </summary>
    [EventRoutingKey("forum.comment.liked")]
    LikedForumComment = 114,

    /// <summary>
    /// New game has been created
    /// </summary>
    [EventRoutingKey("game.created")]
    NewGame = 301,

    /// <summary>
    /// Game has been updated
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
    /// New commentary has been created on game
    /// </summary>
    [EventRoutingKey("game.comment.created")]
    NewGameComment = 331,

    /// <summary>
    /// Game commentary has been updated
    /// </summary>
    [EventRoutingKey("game.comment.changed")]
    ChangedGameComment = 332,

    /// <summary>
    /// Forum commentary has been deleted
    /// </summary>
    [EventRoutingKey("game.comment.deleted")]
    DeletedGameComment = 333,

    /// <summary>
    /// Game commentary has been liked
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
    /// New character has been created
    /// </summary>
    [EventRoutingKey("game.character.created")]
    NewCharacter = 361,

    /// <summary>
    /// Character has been updated
    /// </summary>
    [EventRoutingKey("game.character.updated")]
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
    /// New room has been created
    /// </summary>
    [EventRoutingKey("game.room.created")]
    NewRoom = 381,

    /// <summary>
    /// Room has been updated
    /// </summary>
    [EventRoutingKey("game.room.updated")]
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
    /// New game post has been created
    /// </summary>
    [EventRoutingKey("game.post.created")]
    NewPost = 401,

    /// <summary>
    /// Post has been changed
    /// </summary>
    [EventRoutingKey("game.post.updated")]
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