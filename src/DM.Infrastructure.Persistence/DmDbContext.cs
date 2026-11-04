using DM.Infrastructure.Persistence.Repositories.Search;
using System.Threading.Tasks;
using System.Threading;
using System.Linq.Expressions;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Personal.Notifications;
using DM.Infrastructure.Persistence.ModelConfiguration;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence;

/// <summary>
/// RDB storage context
/// </summary>
public class DmDbContext : DbContext
{
    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SearchableBodies.Refresh(ChangeTracker);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SearchableBodies.Refresh(ChangeTracker);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public DmDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>
    /// Maps to PostgreSQL regexp_replace(input, pattern, replacement, flags).
    /// Used in LINQ queries to strip BBCode blocks (e.g. [private]) before text search.
    /// </summary>
    [DbFunction("regexp_replace", IsBuiltIn = true)]
    public static string RegexpReplace(string input, string pattern, string replacement, string flags)
        => throw new System.NotSupportedException("This method is for EF Core LINQ translation only");

    /// <summary>
    /// The sequence tag numbers are drawn from, named once for the declaration in
    /// ModerationModel and for TagNumbers, which reads it.
    /// </summary>
    internal const string TagShortIdSequence = "TagShortIds";

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var isPostgres = Database.IsNpgsql();

        // The seed is declared in the model so a regenerated migration carries it, and
        // only Migrate builds the schema: EnsureCreated keeps the polymorphic foreign
        // keys that the migration cuts out by hand.
        SearchModel.Configure(modelBuilder, isPostgres);
        AccountModel.Configure(modelBuilder, isPostgres);
        GameModel.Configure(modelBuilder, isPostgres);
        DiceRollModel.Configure(modelBuilder, isPostgres);
        UploadModel.Configure(modelBuilder);
        BlogModel.Configure(modelBuilder);
        ForumModel.Configure(modelBuilder);
        MessagingModel.Configure(modelBuilder);
        ModerationModel.Configure(modelBuilder, isPostgres);
        CommunityModel.Configure(modelBuilder);
        PersonalModel.Configure(modelBuilder, isPostgres);
        SharedModel.Configure(modelBuilder);

        // Global Query Filter: automatically exclude soft-deleted entities.
        // UnreadCounters opts out on purpose: its write path has to see tombstone
        // rows, because an upsert over one revives the marker for a re-added
        // participant, so each of its reads owns its IsRemoved predicate
        // explicitly.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType == typeof(UnreadCounter))
            {
                continue;
            }

            if (typeof(IRemovable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(IRemovable.IsRemoved));
                var filter = Expression.Lambda(Expression.Not(property), parameter);

                // The account filter carries a name so the content reads can lift
                // it alone and keep what removed accounts wrote; see RemovedAuthors.
                if (entityType.ClrType == typeof(User))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .HasQueryFilter(Shared.Users.RemovedAuthors.AccountFilter, filter);
                    continue;
                }

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }

    #region Users

    public DbSet<User> Users { get; set; }

    /// <summary>
    /// User authorization tokens
    /// </summary>
    public DbSet<Token> Tokens { get; set; }

    /// <summary>
    /// User contact information
    /// </summary>
    public DbSet<UserContact> UserContacts { get; set; }

    /// <summary>
    /// Username change history (permanently reserved old usernames)
    /// </summary>
    public DbSet<UsernameHistory> UsernameHistories { get; set; }

    /// <summary>
    /// Username change requests (pending admin approval)
    /// </summary>
    public DbSet<UsernameChangeRequest> UsernameChangeRequests { get; set; }

    /// <summary>
    /// User login records (IP tracking for moderation)
    /// </summary>
    public DbSet<UserLoginRecord> UserLoginRecords { get; set; }

    /// <summary>
    /// Pending registrations (email confirmed, waiting for username selection)
    /// </summary>
    public DbSet<PendingRegistration> PendingRegistrations { get; set; }

    /// <summary>
    /// Authentication sessions (one row per session)
    /// </summary>
    public DbSet<UserSession> UserSessions { get; set; }

    /// <summary>
    /// User settings (one row per user; absence of the row means defaults)
    /// </summary>
    public DbSet<UserSettings> UserSettings { get; set; }

    /// <summary>
    /// Login attempt records (rate limiting and lockout)
    /// </summary>
    public DbSet<LoginAttempt> LoginAttempts { get; set; }

    /// <summary>
    /// Security audit log entries
    /// </summary>
    public DbSet<SecurityAuditEntry> SecurityAuditEntries { get; set; }

    /// <summary>
    /// Second factor state (one row per user; absence of the row means it is off)
    /// </summary>
    public DbSet<UserTwoFactor> UserTwoFactors { get; set; }

    /// <summary>
    /// Recovery codes of the second factor (a fixed set per user)
    /// </summary>
    public DbSet<UserTwoFactorRecoveryCode> UserTwoFactorRecoveryCodes { get; set; }

    /// <summary>
    /// Unfinished logins waiting for a second factor
    /// </summary>
    public DbSet<TwoFactorChallenge> TwoFactorChallenges { get; set; }

    #endregion

    #region Common

    public DbSet<Comment> Comments { get; set; }

    /// <summary>
    /// Comment edit history
    /// </summary>
    public DbSet<CommentEdit> CommentEdits { get; set; }

    public DbSet<Like> Likes { get; set; }

    public DbSet<TagGroup> TagGroups { get; set; }

    /// <summary>
    /// Tags
    /// </summary>
    public DbSet<Tag> Tags { get; set; }

    /// <summary>
    /// Uploads
    /// </summary>
    public DbSet<Upload> Uploads { get; set; }

    /// <summary>
    /// Unread counters (one row per marker)
    /// </summary>
    public DbSet<UnreadCounter> UnreadCounters { get; set; }

    /// <summary>
    /// Notifications (one row per logical notification)
    /// </summary>
    public DbSet<Notification> Notifications { get; set; }

    public DbSet<NotificationRecipient> NotificationRecipients { get; set; }

    /// <summary>
    /// Transactional outbox of domain events
    /// </summary>
    public DbSet<Entities.Outbox.OutboxEvent> OutboxEvents { get; set; }

    #endregion

    #region Subscriptions

    /// <summary>
    /// Content subscriptions
    /// </summary>
    public DbSet<Subscription> Subscriptions { get; set; }

    #endregion

    #region Forum

    public DbSet<Board> Boards { get; set; }

    public DbSet<Topic> Topics { get; set; }

    /// <summary>
    /// Topic edit history
    /// </summary>
    public DbSet<TopicEdit> TopicEdits { get; set; }

    public DbSet<BoardModerator> BoardModerators { get; set; }

    #endregion

    #region Games

    public DbSet<Entities.Game.Game> Games { get; set; }

    public DbSet<GameTag> GameTags { get; set; }

    /// <summary>
    /// Game assistants
    /// </summary>
    public DbSet<GameAssistant> GameAssistants { get; set; }

    /// <summary>
    /// Blacklists
    /// </summary>
    public DbSet<GameBlacklist> GameBlacklists { get; set; }

    /// <summary>
    /// Room access links (characters and readers in rooms)
    /// </summary>
    public DbSet<RoomAccess> RoomAccesses { get; set; }

    public DbSet<Character> Characters { get; set; }

    public DbSet<CharacterAttribute> CharacterAttributes { get; set; }

    /// <summary>
    /// Game rooms
    /// </summary>
    public DbSet<Room> Rooms { get; set; }

    /// <summary>
    /// Game posts
    /// </summary>
    public DbSet<Post> Posts { get; set; }

    /// <summary>
    /// Post edit history
    /// </summary>
    public DbSet<PostEdit> PostEdits { get; set; }

    /// <summary>
    /// Post pendencies (who is expected to post in a room)
    /// </summary>
    public DbSet<PostPendency> PostPendencies { get; set; }

    /// <summary>
    /// Game reviews (reviews of games by players)
    /// </summary>
    public DbSet<GameReview> GameReviews { get; set; }

    /// <summary>
    /// post reviews (reviews of posts with ratings)
    /// </summary>
    public DbSet<PostReview> PostReviews { get; set; }

    /// <summary>
    /// Dice rolls (written only inside the post's transaction)
    /// </summary>
    public DbSet<DiceRoll> DiceRolls { get; set; }

    /// <summary>
    /// Starting character sheets the site offers when a game is created
    /// </summary>
    public DbSet<AttributeSchemaTemplate> AttributeSchemaTemplates { get; set; }

    /// <summary>
    /// Character sheets of games, one row per game
    /// </summary>
    public DbSet<Entities.Game.GameAttributeSchema> GameAttributeSchemas { get; set; }

    #endregion

    #region Messaging

    /// <summary>
    /// Chats
    /// </summary>
    public DbSet<Chat> Chats { get; set; }

    /// <summary>
    /// Chat participants
    /// </summary>
    public DbSet<UserChatLink> UserChatLinks { get; set; }

    /// <summary>
    /// Messages (both global chat and private chats)
    /// </summary>
    public DbSet<Message> Messages { get; set; }

    /// <summary>
    /// Message edit history
    /// </summary>
    public DbSet<MessageEdit> MessageEdits { get; set; }

    public DbSet<GlobalChatEvent> GlobalChatEvents { get; set; }

    public DbSet<GlobalChatEventParticipant> GlobalChatEventParticipants { get; set; }

    #endregion

    #region Administration

    /// <summary>
    /// Complaint tickets
    /// </summary>
    public DbSet<Ticket> Tickets { get; set; }

    /// <summary>
    /// The conversations of tickets. Read by the ticket repository and by the
    /// notifications about tickets only (see the entity)
    /// </summary>
    public DbSet<TicketComment> TicketComments { get; set; }

    public DbSet<Warning> Warnings { get; set; }

    public DbSet<Ban> Bans { get; set; }

    #endregion

    #region Notepads

    /// <summary>
    /// Notepad entries
    /// </summary>
    public DbSet<NotepadEntry> NotepadEntries { get; set; }

    #endregion

    #region User Blacklists

    /// <summary>
    /// User blacklist entries (personal user-to-user blocks)
    /// </summary>
    public DbSet<UserBlacklist> UserBlacklists { get; set; }

    #endregion

    #region Profile Notes

    /// <summary>
    /// Personal notes about other users
    /// </summary>
    public DbSet<UserProfileNote> UserProfileNotes { get; set; }

    /// <summary>
    /// Moderator notes about users (only visible to moderators)
    /// </summary>
    public DbSet<ModeratedProfileNote> ModeratedProfileNotes { get; set; }

    #endregion

    #region Blogs

    /// <summary>
    /// User blogs
    /// </summary>
    public DbSet<Blog> Blogs { get; set; }

    /// <summary>
    /// Blog rubrics (categories)
    /// </summary>
    public DbSet<Rubric> Rubrics { get; set; }

    /// <summary>
    /// Blog publications (posts)
    /// </summary>
    public DbSet<Publication> Publications { get; set; }

    public DbSet<BlogAssistant> BlogAssistants { get; set; }

    /// <summary>
    /// Blog blacklist entries
    /// </summary>
    public DbSet<BlogBlacklist> BlogBlacklists { get; set; }

    #endregion

    #region Community

    /// <summary>
    /// Website testimonials (positive reviews about the website)
    /// </summary>
    public DbSet<WebsiteTestimonial> WebsiteTestimonials { get; set; }

    /// <summary>
    /// User endorsements (positive recommendations between users)
    /// </summary>
    public DbSet<UserEndorsement> UserEndorsements { get; set; }

    /// <summary>
    /// Polls
    /// </summary>
    public DbSet<Poll> Polls { get; set; }

    /// <summary>
    /// Poll options
    /// </summary>
    public DbSet<PollOption> PollOptions { get; set; }

    /// <summary>
    /// Poll votes (one per voter per poll, held by the primary key)
    /// </summary>
    public DbSet<PollVote> PollVotes { get; set; }

    /// <summary>
    /// Website fundraising progress (single-row table, seeded)
    /// </summary>
    public DbSet<FundraisingGoal> FundraisingGoals { get; set; }

    /// <summary>
    /// Award type catalog (timeless). Grant context (year, season,
    /// topic) lives on <see cref="ContestSeries"/>; per-grant on <see cref="UserAward"/>.
    /// </summary>
    public DbSet<AwardType> AwardTypes { get; set; }

    /// <summary>
    /// Literary contest series — one row per (Season, Year). Holds Number,
    /// Variant (Full/Lite) and optional topic URL. Awards reference series.
    /// </summary>
    public DbSet<ContestSeries> ContestSeries { get; set; }

    /// <summary>
    /// Award grants per user.
    /// </summary>
    public DbSet<UserAward> UserAwards { get; set; }

    /// <summary>
    /// Markers linking closed statistics periods to their auto-created
    /// "Итоги …" forum topics (one row per generated digest).
    /// </summary>
    public DbSet<PeriodDigestTopic> PeriodDigestTopics { get; set; }

    /// <summary>
    /// Achievement category catalog, the source of chain metadata
    /// (Title, Description, IconName, Metric, SortOrder, IsActive).
    /// </summary>
    public DbSet<AchievementCategory> AchievementCategories { get; set; }

    /// <summary>
    /// Achievement tier catalog. Each row belongs to a category;
    /// holds tier-specific data (Title, Threshold, Tier).
    /// </summary>
    public DbSet<AchievementType> AchievementTypes { get; set; }

    /// <summary>
    /// Achievement grants per user (auto-evaluated).
    /// </summary>
    public DbSet<UserAchievement> UserAchievements { get; set; }

    #endregion
}
