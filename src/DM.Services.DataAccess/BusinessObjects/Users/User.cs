using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Administration;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.DataContracts;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.DataAccess.BusinessObjects.Games;
using DM.Services.DataAccess.BusinessObjects.Games.Characters;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.DataAccess.BusinessObjects.Games.Posts;
using DM.Services.DataAccess.BusinessObjects.Messaging;
using DM.Services.DataAccess.BusinessObjects.Notepads;
using DM.Services.DataAccess.BusinessObjects.Subscriptions;

namespace DM.Services.DataAccess.BusinessObjects.Users;

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
    public string Login { get; set; } = null!;

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

    /// <summary>
    /// Honorary goblin status (special title for active users)
    /// </summary>
    public bool IsHonorary { get; set; }

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
    /// Password hash algorithm version (1 = SHA256, 2 = PBKDF2)
    /// </summary>
    public int PasswordHashVersion { get; set; } = 1;

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
    /// ICQ number
    /// </summary>
    [MaxLength(20)]
    public string? Icq { get; set; }

    /// <summary>
    /// Skype name
    /// </summary>
    [MaxLength(50)]
    public string? Skype { get; set; }

    /// <summary>
    /// Full user information
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// FK to Upload table for avatar. Null = no avatar.
    /// </summary>
    public Guid? AvatarUploadId { get; set; }

    /// <summary>
    /// Discord ID для будущих уведомлений бота. Не используется для аутентификации.
    /// </summary>
    [MaxLength(50)]
    public string? DiscordId { get; set; }

    /// <summary>
    /// Telegram ID для будущих уведомлений бота. Не используется для аутентификации.
    /// </summary>
    [MaxLength(30)]
    public string? TelegramId { get; set; }

    #region Profile navigations

    /// <summary>
    /// Navigation property to avatar upload
    /// </summary>
    [ForeignKey(nameof(AvatarUploadId))]
    public virtual Upload? AvatarUpload { get; set; }

    /// <summary>
    /// Profile picture (should only be one active)
    /// </summary>
    [InverseProperty(nameof(Upload.UserProfile))]
    public virtual ICollection<Upload> ProfilePictures { get; set; } = [];

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
    /// Login change history
    /// </summary>
    [InverseProperty(nameof(LoginHistory.User))]
    public virtual ICollection<LoginHistory> LoginHistories { get; set; } = [];

    /// <summary>
    /// Login change requests
    /// </summary>
    [InverseProperty(nameof(LoginChangeRequest.User))]
    public virtual ICollection<LoginChangeRequest> LoginChangeRequests { get; set; } = [];

    /// <summary>
    /// Password history entries
    /// </summary>
    [InverseProperty(nameof(PasswordHistory.User))]
    public virtual ICollection<PasswordHistory> PasswordHistories { get; set; } = [];

    #endregion

    #region Common navigations

    /// <summary>
    /// User commentaries
    /// </summary>
    [InverseProperty(nameof(Comment.Author))]
    public virtual ICollection<Comment> Comments { get; set; } = [];

    /// <summary>
    /// User likes
    /// </summary>
    [InverseProperty(nameof(Like.User))]
    public virtual ICollection<Like> Likes { get; set; } = [];

    /// <summary>
    /// User reviews
    /// </summary>
    [InverseProperty(nameof(Review.Author))]
    public virtual ICollection<Review> Reviews { get; set; } = [];

    /// <summary>
    /// Reviews modified by user
    /// </summary>
    [InverseProperty(nameof(Review.ModifiedBy))]
    public virtual ICollection<Review> ReviewsModified { get; set; } = [];

    /// <summary>
    /// Reviews of user's posts (as post author)
    /// </summary>
    [InverseProperty(nameof(Review.PostAuthor))]
    public virtual ICollection<Review> ReviewsAsPostAuthor { get; set; } = [];

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
    [InverseProperty(nameof(Game.Master))]
    public virtual ICollection<Game> GamesAsMaster { get; set; } = [];

    /// <summary>
    /// Games user is GM assistant of
    /// </summary>
    [InverseProperty(nameof(Game.Assistant))]
    public virtual ICollection<Game> GamesAsAssistant { get; set; } = [];

    /// <summary>
    /// Games user moderates
    /// </summary>
    [InverseProperty(nameof(Game.Mentor))]
    public virtual ICollection<Game> GamesAsMentor { get; set; } = [];

    /// <summary>
    /// Games user is blacklisted in
    /// </summary>
    [InverseProperty(nameof(GameBlacklist.User))]
    public virtual ICollection<GameBlacklist> GamesBlacklisted { get; set; } = [];

    /// <summary>
    /// Games observed
    /// </summary>
    [InverseProperty(nameof(Reader.User))]
    public virtual ICollection<Reader> GamesObserved { get; set; } = [];

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
    /// Conversation participations
    /// </summary>
    [InverseProperty(nameof(UserConversationLink.User))]
    public virtual ICollection<UserConversationLink> ConversationLinks { get; set; } = [];

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
    [InverseProperty(nameof(Warning.User))]
    public virtual ICollection<Warning> WarningsReceived { get; set; } = [];

    /// <summary>
    /// Warnings given
    /// </summary>
    [InverseProperty(nameof(Warning.Moderator))]
    public virtual ICollection<Warning> WarningsGiven { get; set; } = [];

    /// <summary>
    /// Bans received
    /// </summary>
    [InverseProperty(nameof(Ban.User))]
    public virtual ICollection<Ban> BansReceived { get; set; } = [];

    /// <summary>
    /// Bans given
    /// </summary>
    [InverseProperty(nameof(Ban.Moderator))]
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