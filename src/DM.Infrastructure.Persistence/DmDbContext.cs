using System.Linq.Expressions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.CrossDomain;
using DM.Infrastructure.Persistence.Entities.DataContracts;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Notepads;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using DM.Infrastructure.Persistence.Entities.Account;
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

        // One active review per user per target (filtered unique index - PostgreSQL only)
        // Platform reviews: one per user
        // User/Game reviews: one per (author, target) pair
        var reviewEntity = modelBuilder.Entity<Review>();
        var reviewIndexBuilder = reviewEntity
            .HasIndex(r => new { r.UserId, r.TargetType, r.TargetId });

        // SQLite doesn't support partial indexes with filters
        if (isPostgres)
        {
            reviewIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }

        reviewIndexBuilder.IsUnique();

        // Index for efficient lookup by target
        var targetIndexBuilder = reviewEntity
            .HasIndex(r => new { r.TargetType, r.TargetId });
        if (isPostgres)
        {
            targetIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }

        // Index for Post reviews by PostAuthorId (for "reviews ON user's posts" queries)
        var postAuthorIndexBuilder = reviewEntity
            .HasIndex(r => r.PostAuthorId);
        if (isPostgres)
        {
            postAuthorIndexBuilder.HasFilter("\"IsRemoved\" = false AND \"PostAuthorId\" IS NOT NULL");
        }

        // Index for Post reviews by GameId (for "reviews in game" queries)
        var gameIndexBuilder = reviewEntity
            .HasIndex(r => r.GameId);
        if (isPostgres)
        {
            gameIndexBuilder.HasFilter("\"IsRemoved\" = false AND \"GameId\" IS NOT NULL");
        }

        // Note: TargetId is a polymorphic reference (User, Game, or Post based on TargetType)
        // Navigation properties are not used - target entities are loaded manually in repositories

        // Configure relationships for soft-deletable and editable entities
        // These have DeletedBy and ModifiedBy navigation properties without inverse collections
        ConfigureSoftDeletableRelationships<Comment>(modelBuilder);
        ConfigureSoftDeletableRelationships<Topic>(modelBuilder);
        ConfigureSoftDeletableRelationships<Message>(modelBuilder);
        ConfigureSoftDeletableRelationships<Post>(modelBuilder);
        ConfigureSoftDeletableRelationships<Character>(modelBuilder);

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
    /// Configure relationships for entities with DeletedBy and ModifiedBy without inverse properties
    /// </summary>
    private static void ConfigureSoftDeletableRelationships<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        var entityBuilder = modelBuilder.Entity<TEntity>();

        // Configure DeletedBy relationship without inverse collection
        entityBuilder
            .HasOne<User>("DeletedBy")
            .WithMany()
            .HasForeignKey("DeletedByUserId")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure ModifiedBy relationship without inverse collection
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
    public DbSet<CommentEditHistory> CommentEditHistory { get; set; }

    /// <summary>
    /// Likes
    /// </summary>
    public DbSet<Like> Likes { get; set; }

    /// <summary>
    /// Reviews
    /// </summary>
    public DbSet<Review> Reviews { get; set; }

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
    public DbSet<Game> Games { get; set; }

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
}