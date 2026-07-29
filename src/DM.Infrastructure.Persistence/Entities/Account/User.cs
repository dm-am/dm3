using DM.Domain.Core.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Dto;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Subscriptions;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for user
/// </summary>
[Table("Users")]
public class User : IUser, IRemovable
{
    /// <inheritdoc />
    [Key]
    public Guid UserId { get; set; }

    /// <inheritdoc />
    [MaxLength(20)]
    public string Username { get; set; } = null!;

    /// <summary>
    /// Registration email (unique)
    /// </summary>
    [MaxLength(100)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Registration moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? LastActivityUtc { get; set; }

    /// <inheritdoc />
    public UserRole Role { get; set; }

    /// <inheritdoc />
    public AccessPolicy AccessPolicy { get; set; }

    /// <summary>
    /// Password salt
    /// </summary>
    [MaxLength(120)]
    public string Salt { get; set; } = null!;

    /// <summary>
    /// Password hash
    /// </summary>
    [MaxLength(300)]
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Password hash algorithm version (4 = Argon2id)
    /// </summary>
    public int PasswordHashVersion { get; set; } = PasswordHashing.CurrentVersion;

    /// <inheritdoc />
    public bool RatingDisabled { get; set; }

    /// <inheritdoc />
    public int QualityRating { get; set; }

    /// <inheritdoc />
    public int QuantityRating { get; set; }

    /// <summary>
    /// Whether the user is a newbie (less than 100 posts).
    /// Computed column based on QuantityRating.
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public bool IsNewbie { get; private set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Custom status
    /// </summary>
    [MaxLength(200)]
    public string? Status { get; set; }

    /// <summary>
    /// Real name
    /// </summary>
    [MaxLength(100)]
    public string? Name { get; set; }

    /// <summary>
    /// Real location
    /// </summary>
    [MaxLength(100)]
    public string? Location { get; set; }

    /// <summary>
    /// User gender
    /// </summary>
    public Gender Gender { get; set; }

    /// <summary>
    /// User birthday date (day and month only, year is ignored)
    /// </summary>
    public DateOnly? BirthdayDate { get; set; }

    /// <summary>
    /// Whether to show birthday to other users
    /// </summary>
    public bool ShowBirthday { get; set; } = true;

    /// <summary>
    /// What the personal blacklist actually does: which surfaces hide a blocked
    /// user's content, and whether direct messages from them are refused.
    /// </summary>
    /// <remarks>
    /// Lives here rather than in the Mongo settings document because the entries
    /// it governs are rows in Postgres with a foreign key: keeping the switch in
    /// the other store made "is this user blocked, and does it apply here" two
    /// reads across two databases with no way to make them agree.
    /// </remarks>
    public UserBlacklistSettings BlacklistSettings { get; set; } = UserBlacklistSettings.Default;

    /// <summary>
    /// Full user information
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// FK to Upload table for avatar. Null = no avatar.
    /// </summary>
    public Guid? AvatarUploadId { get; set; }

    /// <summary>
    /// Discord ID for future bot notifications. Not used for authentication.
    /// </summary>
    [MaxLength(50)]
    public string? DiscordId { get; set; }

    /// <summary>
    /// Telegram ID for future bot notifications. Not used for authentication.
    /// </summary>
    [MaxLength(30)]
    public string? TelegramId { get; set; }

    #region Profile navigations

    /// <summary>
    /// Navigation property to avatar upload
    /// </summary>
    [ForeignKey(nameof(AvatarUploadId))]
    public virtual Upload? AvatarUpload { get; set; }

    // NOTE: ProfilePictures navigation removed - Upload.EntityId is polymorphic without FK constraints

    /// <summary>
    /// Authorization tokens
    /// </summary>
    [InverseProperty(nameof(Token.User))]
    public virtual ICollection<Token> Tokens { get; set; } = [];

    /// <summary>
    /// User contact information (flexible type+value pairs)
    /// </summary>
    [InverseProperty(nameof(UserContact.User))]
    public virtual ICollection<UserContact> Contacts { get; set; } = [];

    /// <summary>
    /// Username change history
    /// </summary>
    [InverseProperty(nameof(UsernameHistory.User))]
    public virtual ICollection<UsernameHistory> UsernameHistories { get; set; } = [];

    /// <summary>
    /// Username change requests
    /// </summary>
    [InverseProperty(nameof(UsernameChangeRequest.User))]
    public virtual ICollection<UsernameChangeRequest> UsernameChangeRequests { get; set; } = [];

    #endregion

    #region Common navigations

    /// <summary>
    /// User comments
    /// </summary>
    [InverseProperty(nameof(Comment.Author))]
    public virtual ICollection<Comment> Comments { get; set; } = [];

    /// <summary>
    /// User likes
    /// </summary>
    [InverseProperty(nameof(Like.User))]
    public virtual ICollection<Like> Likes { get; set; } = [];

    /// <summary>
    /// User endorsements authored
    /// </summary>
    [InverseProperty(nameof(UserEndorsement.Author))]
    public virtual ICollection<UserEndorsement> UserEndorsementsAuthored { get; set; } = [];

    /// <summary>
    /// User endorsements received
    /// </summary>
    [InverseProperty(nameof(UserEndorsement.TargetUser))]
    public virtual ICollection<UserEndorsement> UserEndorsementsReceived { get; set; } = [];

    /// <summary>
    /// Website testimonials authored
    /// </summary>
    [InverseProperty(nameof(WebsiteTestimonial.Author))]
    public virtual ICollection<WebsiteTestimonial> WebsiteTestimonials { get; set; } = [];

    /// <summary>
    /// Game reviews authored
    /// </summary>
    [InverseProperty(nameof(GameReview.Author))]
    public virtual ICollection<GameReview> GameReviews { get; set; } = [];

    /// <summary>
    /// post reviews authored
    /// </summary>
    [InverseProperty(nameof(PostReview.Author))]
    public virtual ICollection<PostReview> PostReviewsAuthored { get; set; } = [];

    /// <summary>
    /// post reviews of user's posts (as post author)
    /// </summary>
    [InverseProperty(nameof(PostReview.PostAuthor))]
    public virtual ICollection<PostReview> PostReviewsReceived { get; set; } = [];

    /// <summary>
    /// User uploads
    /// </summary>
    [InverseProperty(nameof(Upload.Owner))]
    public virtual ICollection<Upload> Uploads { get; set; } = [];

    #endregion

    #region Forum navigations

    /// <summary>
    /// User topics
    /// </summary>
    [InverseProperty(nameof(Topic.Author))]
    public virtual ICollection<Topic> Topics { get; set; } = [];

    /// <summary>
    /// User moderation links
    /// </summary>
    [InverseProperty(nameof(BoardModerator.User))]
    public virtual ICollection<BoardModerator> BoardModerators { get; set; } = [];

    #endregion

    #region Game navigations

    /// <summary>
    /// Games user is GM of
    /// </summary>
    [InverseProperty(nameof(Game.Game.Master))]
    public virtual ICollection<Game.Game> GamesAsMaster { get; set; } = [];

    /// <summary>
    /// Game assistant links
    /// </summary>
    [InverseProperty(nameof(GameAssistant.User))]
    public virtual ICollection<GameAssistant> GameAssistants { get; set; } = [];

    /// <summary>
    /// Games user moderates
    /// </summary>
    [InverseProperty(nameof(Game.Game.Mentor))]
    public virtual ICollection<Game.Game> GamesAsMentor { get; set; } = [];

    /// <summary>
    /// Games user is blacklisted in
    /// </summary>
    [InverseProperty(nameof(GameBlacklist.BlockedUser))]
    public virtual ICollection<GameBlacklist> GamesBlacklisted { get; set; } = [];

    /// <summary>
    /// Characters
    /// </summary>
    [InverseProperty(nameof(Character.Author))]
    public virtual ICollection<Character> Characters { get; set; } = [];

    /// <summary>
    /// Posts
    /// </summary>
    [InverseProperty(nameof(Post.Author))]
    public virtual ICollection<Post> Posts { get; set; } = [];

    /// <summary>
    /// Post pendencies created by user (user is waiting for someone else to post)
    /// </summary>
    [InverseProperty(nameof(PostPendency.CreatedBy))]
    public virtual ICollection<PostPendency> PostPendenciesCreated { get; set; } = [];

    /// <summary>
    /// Post pendencies where user is expected to post
    /// </summary>
    [InverseProperty(nameof(PostPendency.WaitingForUser))]
    public virtual ICollection<PostPendency> PostPendenciesWaitingFor { get; set; } = [];

    #endregion

    #region Messaging navigations

    /// <summary>
    /// Chat participations
    /// </summary>
    [InverseProperty(nameof(UserChatLink.User))]
    public virtual ICollection<UserChatLink> ChatLinks { get; set; } = [];

    /// <summary>
    /// Messages
    /// </summary>
    [InverseProperty(nameof(Message.Author))]
    public virtual ICollection<Message> Messages { get; set; } = [];

    #endregion

    #region Administration navigations

    /// <summary>
    /// Tickets filed by user
    /// </summary>
    [InverseProperty(nameof(Ticket.Author))]
    public virtual ICollection<Ticket> TicketsFiled { get; set; } = [];

    /// <summary>
    /// Tickets against user
    /// </summary>
    [InverseProperty(nameof(Ticket.Target))]
    public virtual ICollection<Ticket> TicketsAgainst { get; set; } = [];

    /// <summary>
    /// Tickets answered by user
    /// </summary>
    [InverseProperty(nameof(Ticket.AnswerAuthor))]
    public virtual ICollection<Ticket> TicketsAnswered { get; set; } = [];

    /// <summary>
    /// Warnings received
    /// </summary>
    [InverseProperty(nameof(Warning.TargetUser))]
    public virtual ICollection<Warning> WarningsReceived { get; set; } = [];

    /// <summary>
    /// Warnings given (authored by this moderator)
    /// </summary>
    [InverseProperty(nameof(Warning.Author))]
    public virtual ICollection<Warning> WarningsGiven { get; set; } = [];

    /// <summary>
    /// Bans received
    /// </summary>
    [InverseProperty(nameof(Ban.TargetUser))]
    public virtual ICollection<Ban> BansReceived { get; set; } = [];

    /// <summary>
    /// Bans given (authored by this moderator)
    /// </summary>
    [InverseProperty(nameof(Ban.Author))]
    public virtual ICollection<Ban> BansGiven { get; set; } = [];

    /// <summary>
    /// Login records (IP addresses and user agents for moderation)
    /// </summary>
    [InverseProperty(nameof(UserLoginRecord.User))]
    public virtual ICollection<UserLoginRecord> LoginRecords { get; set; } = [];

    #endregion

    #region Subscription navigations

    /// <summary>
    /// User subscriptions
    /// </summary>
    [InverseProperty(nameof(Subscription.Subscriber))]
    public virtual ICollection<Subscription> Subscriptions { get; set; } = [];

    /// <summary>
    /// Notepad entries authored by user
    /// </summary>
    [InverseProperty(nameof(NotepadEntry.Author))]
    public virtual ICollection<NotepadEntry> NotepadEntries { get; set; } = [];

    /// <summary>
    /// Notepad categories authored by user
    /// </summary>
    [InverseProperty(nameof(NotepadCategory.Author))]
    public virtual ICollection<NotepadCategory> NotepadCategories { get; set; } = [];

    /// <summary>
    /// User's personal blacklist (users this user has blocked)
    /// </summary>
    [InverseProperty(nameof(UserBlacklist.Owner))]
    public virtual ICollection<UserBlacklist> BlacklistedUsers { get; set; } = [];

    /// <summary>
    /// Users who have blocked this user
    /// </summary>
    [InverseProperty(nameof(UserBlacklist.BlockedUser))]
    public virtual ICollection<UserBlacklist> BlockedByUsers { get; set; } = [];

    #endregion
}