using System.Linq.Expressions;
using DM.Domain.Core.Enums;
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
using DM.Infrastructure.Persistence.Entities.Community;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence;

/// <summary>
/// RDB storage context
/// </summary>
public class DmDbContext : DbContext
{
    /// <inheritdoc />
    public DmDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var isPostgres = Database.IsNpgsql();

        #region UserEndorsement Indexes

        // One active endorsement per author-target pair
        var userEndorsementIndexBuilder = modelBuilder.Entity<UserEndorsement>()
            .HasIndex(e => new { e.AuthorId, e.TargetUserId });
        if (isPostgres)
        {
            userEndorsementIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }
        userEndorsementIndexBuilder.IsUnique();

        // Index for efficient lookup by target user (endorsements received)
        var endorsementTargetIndexBuilder = modelBuilder.Entity<UserEndorsement>()
            .HasIndex(e => e.TargetUserId);
        if (isPostgres)
        {
            endorsementTargetIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }

        #endregion

        #region WebsiteTestimonial Indexes

        // One testimonial per user
        var testimonialIndexBuilder = modelBuilder.Entity<WebsiteTestimonial>()
            .HasIndex(t => t.AuthorId);
        if (isPostgres)
        {
            testimonialIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }
        testimonialIndexBuilder.IsUnique();

        #endregion

        #region GameReview Indexes

        // One active review per author-game pair
        var gameReviewIndexBuilder = modelBuilder.Entity<GameReview>()
            .HasIndex(r => new { r.AuthorId, r.GameId });
        if (isPostgres)
        {
            gameReviewIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }
        gameReviewIndexBuilder.IsUnique();

        // Index for efficient lookup by game (reviews of a game)
        var gameReviewByGameIndexBuilder = modelBuilder.Entity<GameReview>()
            .HasIndex(r => r.GameId);
        if (isPostgres)
        {
            gameReviewByGameIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }

        #endregion

        #region PostReview Indexes

        // One active review per author-post pair
        var ratedPostReviewIndexBuilder = modelBuilder.Entity<PostReview>()
            .HasIndex(r => new { r.AuthorId, r.PostId });
        if (isPostgres)
        {
            ratedPostReviewIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }
        ratedPostReviewIndexBuilder.IsUnique();

        // Index for reviews by PostAuthorId (for "reviews ON user's posts" queries)
        var postReviewByAuthorIndexBuilder = modelBuilder.Entity<PostReview>()
            .HasIndex(r => r.PostAuthorId);
        if (isPostgres)
        {
            postReviewByAuthorIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }

        // Index for reviews by GameId (for "reviews in game" queries)
        var postReviewByGameIndexBuilder = modelBuilder.Entity<PostReview>()
            .HasIndex(r => r.GameId);
        if (isPostgres)
        {
            postReviewByGameIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }

        // Index for reviews by PostId (for "reviews of a post" queries)
        var postReviewByPostIndexBuilder = modelBuilder.Entity<PostReview>()
            .HasIndex(r => r.PostId);
        if (isPostgres)
        {
            postReviewByPostIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }

        #endregion

        // Configure DeletedBy relationships for soft-deletable entities (no inverse collections)
        ConfigureDeletedByRelationship<Comment>(modelBuilder);
        ConfigureDeletedByRelationship<Topic>(modelBuilder);
        ConfigureDeletedByRelationship<Message>(modelBuilder);
        ConfigureDeletedByRelationship<Post>(modelBuilder);
        ConfigureDeletedByRelationship<Character>(modelBuilder);

        // Configure both DeletedBy and ModifiedBy for editable entities (no inverse collections)
        ConfigureEditableRelationships<GameReview>(modelBuilder);
        ConfigureEditableRelationships<PostReview>(modelBuilder);
        ConfigureEditableRelationships<UserEndorsement>(modelBuilder);
        ConfigureEditableRelationships<WebsiteTestimonial>(modelBuilder);

        // Token has 3 user relationships: User (owner), Creator, DeletedBy
        modelBuilder.Entity<Token>()
            .HasOne(t => t.User)
            .WithMany(u => u.Tokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Token>()
            .HasOne(t => t.Creator)
            .WithMany()
            .HasForeignKey(t => t.CreatorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Token>()
            .HasOne(t => t.DeletedBy)
            .WithMany()
            .HasForeignKey(t => t.DeletedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Comment.EntityId is a polymorphic reference - it can point to Topic, Game, Blog, or Publication.
        // Blog.Comments, Game.Comments, Publication.Comments are [NotMapped] to prevent shadow FK creation.
        // Only Topic.Comments is a real EF relationship, configured here.
        modelBuilder.Entity<Topic>()
            .HasMany(t => t.Comments)
            .WithOne(c => c.Topic)
            .HasForeignKey(c => c.EntityId)
            .OnDelete(DeleteBehavior.ClientCascade);

        // Configure IsNewbie as a computed column
        if (isPostgres)
        {
            modelBuilder.Entity<User>()
                .Property(u => u.IsNewbie)
                .HasComputedColumnSql("\"QuantityRating\" < 100", stored: true);
        }

        // Configure AvatarUpload relationship
        modelBuilder.Entity<User>()
            .HasOne(u => u.AvatarUpload)
            .WithMany()
            .HasForeignKey(u => u.AvatarUploadId)
            .OnDelete(DeleteBehavior.SetNull);

        // OldUsername is globally unique — permanently reserved usernames cannot be reused
        modelBuilder.Entity<UsernameHistory>()
            .HasIndex(h => h.OldUsername).IsUnique();

        // Only one pending request per user
        if (isPostgres)
        {
            modelBuilder.Entity<UsernameChangeRequest>()
                .HasIndex(r => r.UserId)
                .HasFilter("\"Status\" = 0")
                .IsUnique();
        }

        // Unique index on UserBlacklist(OwnerId, BlockedUserId) - prevent duplicate blacklist entries
        modelBuilder.Entity<UserBlacklist>()
            .HasIndex(b => new { b.OwnerId, b.BlockedUserId }).IsUnique();

        // Unique index on UserProfileNote(OwnerId, SubjectUserId) - prevent duplicate profile notes
        modelBuilder.Entity<UserProfileNote>()
            .HasIndex(n => new { n.OwnerId, n.SubjectUserId }).IsUnique();

        // Case-insensitive username and email indexes for efficient lookups in PostgreSQL
        if (isPostgres)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .HasDatabaseName("IX_Users_Username_Lower")
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .HasDatabaseName("IX_Users_Email_Lower")
                .IsUnique();
        }

        // Composite index on Token(UserId, Type) - for frequent "find user's tokens by type" queries
        modelBuilder.Entity<Token>()
            .HasIndex(t => new { t.UserId, t.Type });

        // UserLoginRecord indexes for moderation IP tracking
        modelBuilder.Entity<UserLoginRecord>(entity =>
        {
            // Efficient lookup of a user's login history (sorted by date descending)
            entity.HasIndex(r => new { r.UserId, r.LoginUtc })
                .HasDatabaseName("ix_user_login_records_user_date");

            // Efficient search by IP address (for linked profiles detection)
            entity.HasIndex(r => r.IpAddress)
                .HasDatabaseName("ix_user_login_records_ip");

            entity.HasOne(r => r.User)
                .WithMany(u => u.LoginRecords)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PendingRegistration indexes for email-first registration flow
        modelBuilder.Entity<PendingRegistration>(entity =>
        {
            // One pending registration per email
            entity.HasIndex(p => p.Email)
                .HasDatabaseName("IX_PendingRegistrations_Email")
                .IsUnique();

            // Fast lookup by activation token
            entity.HasIndex(p => p.TokenId)
                .HasDatabaseName("IX_PendingRegistrations_TokenId")
                .IsUnique();

            // For cleanup of old pending registrations (>7 days)
            entity.HasIndex(p => p.CreatedUtc)
                .HasDatabaseName("IX_PendingRegistrations_CreatedUtc");
        });

        // NOTE: Upload.EntityId is a polymorphic FK (points to User, Game, Character, or Post)
        // depending on UploadType. No navigation properties or FK constraints are defined
        // because the same column cannot have FK constraints to multiple tables.

        // Global Query Filter: automatically exclude soft-deleted entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IRemovable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(IRemovable.IsRemoved));
                var filter = Expression.Lambda(Expression.Not(property), parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }

    /// <summary>
    /// Configure DeletedBy relationship for ISoftDeletable entities without inverse properties
    /// </summary>
    private static void ConfigureDeletedByRelationship<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .HasOne<User>("DeletedBy")
            .WithMany()
            .HasForeignKey("DeletedByUserId")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>
    /// Configure DeletedBy and ModifiedBy relationships for editable entities without inverse properties
    /// </summary>
    private static void ConfigureEditableRelationships<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        var entityBuilder = modelBuilder.Entity<TEntity>();

        // Configure DeletedBy relationship
        entityBuilder
            .HasOne<User>("DeletedBy")
            .WithMany()
            .HasForeignKey("DeletedByUserId")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure ModifiedBy relationship
        entityBuilder
            .HasOne<User>("ModifiedBy")
            .WithMany()
            .HasForeignKey("ModifiedByUserId")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }

    #region Users

    /// <summary>
    /// Users
    /// </summary>
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

    #endregion

    #region Common

    /// <summary>
    /// comments
    /// </summary>
    public DbSet<Comment> Comments { get; set; }

    /// <summary>
    /// Comment edit history
    /// </summary>
    public DbSet<CommentEdit> CommentEdits { get; set; }

    /// <summary>
    /// Likes
    /// </summary>
    public DbSet<Like> Likes { get; set; }

    /// <summary>
    /// Tag groups
    /// </summary>
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
    /// Outbox events
    /// </summary>
    public DbSet<OutboxEvent> OutboxEvents { get; set; }

    #endregion

    #region Subscriptions

    /// <summary>
    /// Content subscriptions
    /// </summary>
    public DbSet<Subscription> Subscriptions { get; set; }

    #endregion

    #region Forum

    /// <summary>
    /// Boards
    /// </summary>
    public DbSet<Board> Boards { get; set; }

    /// <summary>
    /// Topics
    /// </summary>
    public DbSet<Topic> Topics { get; set; }

    /// <summary>
    /// Topic edit history
    /// </summary>
    public DbSet<TopicEdit> TopicEdits { get; set; }

    /// <summary>
    /// Board Moderators
    /// </summary>
    public DbSet<BoardModerator> BoardModerators { get; set; }

    #endregion

    #region Games

    /// <summary>
    /// Games
    /// </summary>
    public DbSet<Entities.Game.Game> Games { get; set; }

    /// <summary>
    /// Game tags
    /// </summary>
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

    /// <summary>
    /// Characters
    /// </summary>
    public DbSet<Character> Characters { get; set; }

    /// <summary>
    /// Character edit history
    /// </summary>
    public DbSet<CharacterEdit> CharacterEdits { get; set; }

    /// <summary>
    /// Character attribute values
    /// </summary>
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
    /// post reviews (reviews of posts with ratings and likes)
    /// </summary>
    public DbSet<PostReview> PostReviews { get; set; }

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

    /// <summary>
    /// Global chat events
    /// </summary>
    public DbSet<GlobalChatEvent> GlobalChatEvents { get; set; }

    /// <summary>
    /// Global chat event participants
    /// </summary>
    public DbSet<GlobalChatEventParticipant> GlobalChatEventParticipants { get; set; }

    #endregion

    #region Administration

    /// <summary>
    /// Complaint tickets
    /// </summary>
    public DbSet<Ticket> Tickets { get; set; }

    /// <summary>
    /// Ticket responses (conversation history)
    /// </summary>
    public DbSet<TicketResponse> TicketResponses { get; set; }

    /// <summary>
    /// Warnings
    /// </summary>
    public DbSet<Warning> Warnings { get; set; }

    /// <summary>
    /// Bans
    /// </summary>
    public DbSet<Ban> Bans { get; set; }

    #endregion

    #region Notepads

    /// <summary>
    /// Notepad entries
    /// </summary>
    public DbSet<NotepadEntry> NotepadEntries { get; set; }

    /// <summary>
    /// Notepad categories
    /// </summary>
    public DbSet<NotepadCategory> NotepadCategories { get; set; }

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

    /// <summary>
    /// Blog assistants
    /// </summary>
    public DbSet<BlogAssistant> BlogAssistants { get; set; }

    /// <summary>
    /// Blog blacklist entries
    /// </summary>
    public DbSet<BlogBlacklist> BlogBlacklists { get; set; }

    /// <summary>
    /// Rubric access entries
    /// </summary>
    public DbSet<RubricAccess> RubricAccesses { get; set; }

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

    #endregion
}
