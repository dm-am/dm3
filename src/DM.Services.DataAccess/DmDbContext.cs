using DM.Services.DataAccess.BusinessObjects.Administration;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.DataAccess.BusinessObjects.Games;
using DM.Services.DataAccess.BusinessObjects.Games.Characters;
using DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.DataAccess.BusinessObjects.Games.Posts;
using DM.Services.DataAccess.BusinessObjects.Games.Rating;
using DM.Services.DataAccess.BusinessObjects.Messaging;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.DataAccess;

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

        // Configure OpenIddict only for PostgreSQL (not SQLite in tests)
        if (isPostgres)
        {
            modelBuilder.UseOpenIddict();
        }

        // One active review per user (filtered unique index - PostgreSQL only)
        var reviewBuilder = modelBuilder.Entity<Review>()
            .HasIndex(r => r.UserId);

        // SQLite doesn't support partial indexes with filters
        if (isPostgres)
        {
            reviewBuilder.HasFilter("\"IsRemoved\" = false");
        }

        reviewBuilder.IsUnique();

        // Configure relationships for soft-deletable and editable entities
        // These have DeletedBy and ModifiedBy navigation properties without inverse collections
        ConfigureSoftDeletableRelationships<Comment>(modelBuilder);
        ConfigureSoftDeletableRelationships<ForumTopic>(modelBuilder);
        ConfigureSoftDeletableRelationships<Message>(modelBuilder);
        ConfigureSoftDeletableRelationships<Post>(modelBuilder);
        ConfigureSoftDeletableRelationships<Character>(modelBuilder);
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

    #endregion

    #region Common

    /// <summary>
    /// Commentaries
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

    #region Forum

    /// <summary>
    /// Boards
    /// </summary>
    public DbSet<Board> Boards { get; set; }

    /// <summary>
    /// Topics
    /// </summary>
    public DbSet<ForumTopic> ForumTopics { get; set; }

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
    /// Game readers
    /// </summary>
    public DbSet<Reader> Readers { get; set; }

    /// <summary>
    /// Blacklists
    /// </summary>
    public DbSet<BlackListLink> BlackListLinks { get; set; }

    /// <summary>
    /// Characters in rooms
    /// </summary>
    public DbSet<RoomClaim> RoomClaims { get; set; }

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
    /// Game post anticipations
    /// </summary>
    public DbSet<PendingPost> PendingPosts { get; set; }

    /// <summary>
    /// Game post rating votes
    /// </summary>
    public DbSet<Vote> Votes { get; set; }

    #endregion

    #region Messaging

    /// <summary>
    /// Conversations
    /// </summary>
    public DbSet<Conversation> Conversations { get; set; }

    /// <summary>
    /// Conversations participants
    /// </summary>
    public DbSet<UserConversationLink> UserConversationLinks { get; set; }

    /// <summary>
    /// Messages (both chat and private conversations)
    /// </summary>
    public DbSet<Message> Messages { get; set; }

    /// <summary>
    /// Message edit history
    /// </summary>
    public DbSet<MessageEdit> MessageEdits { get; set; }

    #endregion

    #region Administration

    /// <summary>
    /// Complaints
    /// </summary>
    public DbSet<Report> Reports { get; set; }

    /// <summary>
    /// Warnings
    /// </summary>
    public DbSet<Warning> Warnings { get; set; }

    /// <summary>
    /// Bans
    /// </summary>
    public DbSet<Ban> Bans { get; set; }

    #endregion
}