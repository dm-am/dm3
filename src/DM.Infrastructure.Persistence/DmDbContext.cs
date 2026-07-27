using System;
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
using NpgsqlTypes;

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

    /// <summary>
    /// Maps to PostgreSQL regexp_replace(input, pattern, replacement, flags).
    /// Used in LINQ queries to strip BBCode blocks (e.g. [private]) before text search.
    /// </summary>
    [DbFunction("regexp_replace", IsBuiltIn = true)]
    public static string RegexpReplace(string input, string pattern, string replacement, string flags)
        => throw new System.NotSupportedException("This method is for EF Core LINQ translation only");

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

        #region FundraisingGoal

        // Single-row table with the website fundraising progress.
        // Seeded with a fixed GUID (zero-family, block 0005) so that
        // GET always has a row and repeated seeding never creates duplicates.
        modelBuilder.Entity<FundraisingGoal>(entity =>
        {
            entity.Property(g => g.GoalAmount).HasPrecision(18, 2);
            entity.Property(g => g.CollectedAmount).HasPrecision(18, 2);

            entity.HasOne(g => g.UpdatedBy)
                .WithMany()
                .HasForeignKey(g => g.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasData(new FundraisingGoal
            {
                FundraisingGoalId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                GoalAmount = 50000m,
                CollectedAmount = 17000m,
                ModifiedUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
            });
        });

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

        #region Statistics period indexes

        // Partial CreatedUtc indexes backing the statistics period aggregates
        // (leaderboards group live rows by a [start, end) CreatedUtc window on
        // Posts, PostReviews and Publications) — same live-rows-only partial
        // index idiom as the review indexes above.
        var postByCreatedIndexBuilder = modelBuilder.Entity<Post>()
            .HasIndex(p => p.CreatedUtc);
        if (isPostgres)
        {
            postByCreatedIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }

        var postReviewByCreatedIndexBuilder = modelBuilder.Entity<PostReview>()
            .HasIndex(r => r.CreatedUtc);
        if (isPostgres)
        {
            postReviewByCreatedIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }

        var publicationByCreatedIndexBuilder = modelBuilder.Entity<Publication>()
            .HasIndex(p => p.CreatedUtc);
        if (isPostgres)
        {
            publicationByCreatedIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }

        // Period digest markers: one digest per calendar month and one per
        // year. NULLs are distinct in unique indexes (PG and SQLite alike),
        // so the two shapes need separate partial unique indexes: monthly
        // digests unique on (Year, Month), the yearly digest unique on Year
        // alone. The generator also checks before inserting; the indexes are
        // the concurrency backstop.
        modelBuilder.Entity<PeriodDigestTopic>(entity =>
        {
            entity.HasIndex(d => new { d.Year, d.Month })
                .IsUnique()
                .HasFilter(isPostgres ? "\"Month\" IS NOT NULL" : "Month IS NOT NULL")
                .HasDatabaseName("IX_PeriodDigestTopics_Year_Month");

            entity.HasIndex(d => d.Year)
                .IsUnique()
                .HasFilter(isPostgres ? "\"Month\" IS NULL" : "Month IS NULL")
                .HasDatabaseName("IX_PeriodDigestTopics_Year");
        });

        #endregion

        // Configure DeletedBy relationships for soft-deletable entities (no inverse collections)
        ConfigureDeletedByRelationship<Comment>(modelBuilder);
        ConfigureDeletedByRelationship<Topic>(modelBuilder);
        ConfigureDeletedByRelationship<Message>(modelBuilder);
        ConfigureDeletedByRelationship<Post>(modelBuilder);
        ConfigureDeletedByRelationship<Character>(modelBuilder);

        #region Full-text search (PostgreSQL tsvector)

        // Generated STORED tsvector columns + GIN indexes power the unified
        // message/post search (GET /v1/search/messages). The Post expression
        // strips [private=…]…[/private] blocks BEFORE indexing, so private text
        // is never tokenized and can never be matched or previewed by anyone.
        // regexp_replace + explicit-config to_tsvector are IMMUTABLE, so both
        // are valid inside a generated column. Shadow "SearchVector" properties
        // keep the tsvector off the domain-facing entity surface.
        if (isPostgres)
        {
            modelBuilder.Entity<Message>(b =>
            {
                b.Property<NpgsqlTsVector>("SearchVector")
                    .HasComputedColumnSql("to_tsvector('russian', coalesce(\"Text\", ''))", stored: true);
                b.HasIndex("SearchVector").HasMethod("gin");
            });

            modelBuilder.Entity<Post>(b =>
            {
                b.Property<NpgsqlTsVector>("SearchVector")
                    .HasComputedColumnSql(
                        "to_tsvector('russian', regexp_replace(coalesce(\"GameText\", ''), " +
                        "'\\[private(=[^\\]]*)?\\][\\s\\S]*?\\[/private\\]', ' ', 'gi'))",
                        stored: true);
                b.HasIndex("SearchVector").HasMethod("gin").HasDatabaseName("IX_Posts_SearchVector");
            });
        }

        // Composite index backing message cursor/keyset pagination and search
        // (filter by ChatId, order by CreatedUtc, tie-break by MessageId).
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.ChatId, m.CreatedUtc, m.MessageId });

        #endregion

        // Configure both DeletedBy and ModifiedBy for editable entities (no inverse collections)
        ConfigureEditableRelationships<GameReview>(modelBuilder);
        ConfigureEditableRelationships<PostReview>(modelBuilder);
        ConfigureEditableRelationships<UserEndorsement>(modelBuilder);
        ConfigureEditableRelationships<WebsiteTestimonial>(modelBuilder);
        ConfigureDeletedByRelationship<UserAward>(modelBuilder);

        // ---- Awards / Achievements: schema + indexes + seed ----

        modelBuilder.Entity<AwardType>()
            .HasIndex(t => t.Code)
            .IsUnique();

        modelBuilder.Entity<ContestSeries>(entity =>
        {
            // Each contest type has its own sequential numbering.
            // Series identity is the (ContestType, Number) pair. Year is purely
            // a display field and is not part of the UNIQUE.
            entity.HasIndex(s => new { s.ContestType, s.Number }).IsUnique();
        });

        modelBuilder.Entity<UserAward>(entity =>
        {
            // The awarder navigation is configured separately to avoid
            // multiple cascade paths: AwardedByUserId points to User,
            // which already has Subscribers/etc. No collection on User.
            entity.HasOne(a => a.AwardedBy)
                .WithMany()
                .HasForeignKey(a => a.AwardedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.AwardType)
                .WithMany()
                .HasForeignKey(a => a.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ContestSeries is optional — if the series is deleted/deactivated,
            // the award remains orphan history without a link to the contest context.
            entity.HasOne(a => a.ContestSeries)
                .WithMany(s => s.Awards)
                .HasForeignKey(a => a.ContestSeriesId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for efficient retrieval of a user's awards.
            // The !IsRemoved filter is handled by the global soft-delete filter,
            // but the constraint is still useful in the index — a partial index
            // saves space and speeds up range scans.
            var awardIdx = entity.HasIndex(a => new { a.UserId, a.AwardedUtc });
            if (isPostgres) awardIdx.HasFilter("\"IsRemoved\" = false");
        });

        modelBuilder.Entity<AchievementCategory>(entity =>
        {
            entity.HasIndex(c => c.Code).IsUnique();
            // One metric — one category. Protects from an accidental duplicate chain.
            entity.HasIndex(c => c.Metric).IsUnique();
        });

        modelBuilder.Entity<AchievementType>(entity =>
        {
            entity.HasIndex(t => t.Code).IsUnique();

            entity.HasOne(t => t.Category)
                .WithMany(c => c.Types)
                .HasForeignKey(t => t.AchievementCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.AchievementType)
                .WithMany()
                .HasForeignKey(a => a.AchievementTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // KEY invariant: a given achievement is granted to a user
            // exactly once. A UNIQUE violation in TryGrantAsync
            // is silently treated as "already there" — which is exactly the evaluator's
            // idempotency under races between lazy-eval and the event worker.
            var achievementUniqueIdx = entity.HasIndex(a => new { a.UserId, a.AchievementTypeId }).IsUnique();
            if (isPostgres) achievementUniqueIdx.HasFilter("\"IsRemoved\" = false");

            entity.HasIndex(a => new { a.UserId, a.EarnedUtc });
        });

        // ---- Bootstrap seed: exactly one record per catalog ----
        // Fixed GUIDs (the zero family) — so a repeated seed does not
        // create duplicates and code outside migrations can reference these
        // records by a predictable ID when needed.
        // The full seed (52 achievement types, 13 categories, 6 award types,
        // 2 series) lives in InitialCreate.cs via InsertData.

        var seedAwardId = Guid.Parse("00000000-0000-0000-0001-000000000001");
        var seedCategoryId = Guid.Parse("00000000-0000-0000-0003-000000000002");
        var seedAchievementId = Guid.Parse("00000000-0000-0000-0002-000000000001");

        modelBuilder.Entity<AwardType>().HasData(new AwardType
        {
            AwardTypeId = seedAwardId,
            Code = "contest_first",
            Title = "Литконкурс",
            Description = "Победитель конкурса",
            IconName = "trophy-cup",
            Tier = 1,
            SortOrder = 1,
            IsActive = true,
        });

        modelBuilder.Entity<AchievementCategory>().HasData(new AchievementCategory
        {
            AchievementCategoryId = seedCategoryId,
            Code = "game_posts_authored",
            Title = "Игровые посты",
            Description = "Игровые посты в активных играх. Считаются все, включая удаленные игры.",
            IconName = "scroll-quill",
            Metric = AchievementMetric.GamePostsAuthored,
            SortOrder = 2,
            IsActive = true,
        });

        modelBuilder.Entity<AchievementType>().HasData(new AchievementType
        {
            AchievementTypeId = seedAchievementId,
            Code = "POSTS_100",
            Title = "Автор",
            Threshold = 100,
            Tier = 1,
            AchievementCategoryId = seedCategoryId,
        });

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
        //
        // ── IMPORTANT for migration regeneration ──
        // From this `HasMany().WithOne()` pair EF generates a DB-level FK
        // `FK_Comments_Topics_EntityId`. This constraint is FALSE: comments on Game/Blog/
        // Publication have an EntityId not from Topics, and INSERT fails on its check.
        // In the generated migration file this `migrationBuilder.AddForeignKey`
        // block must be removed manually (see the NOTE comment in InitialCreate.cs).
        // Referential integrity is maintained by application logic.
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

        // Upload target FKs: typed nullable columns (TargetUserId,
        // TargetCharacterId, TargetPostId), exactly one non-null. Each
        // with an FK constraint and an index for batched-query lookups.
        modelBuilder.Entity<Upload>(entity =>
        {
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(u => u.TargetUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            entity.HasOne<DM.Infrastructure.Persistence.Entities.Game.Characters.Character>()
                .WithMany()
                .HasForeignKey(u => u.TargetCharacterId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            entity.HasOne<DM.Infrastructure.Persistence.Entities.Game.Posts.Post>()
                .WithMany()
                .HasForeignKey(u => u.TargetPostId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            entity.HasIndex(u => u.TargetUserId)
                .HasFilter("\"TargetUserId\" IS NOT NULL");
            entity.HasIndex(u => u.TargetCharacterId)
                .HasFilter("\"TargetCharacterId\" IS NOT NULL");
            entity.HasIndex(u => u.TargetPostId)
                .HasFilter("\"TargetPostId\" IS NOT NULL");

            // CHECK constraint: exactly one typed target column
            // is non-null AND matches the Type discriminator.
            // CHECK: exactly one typed target column is non-null AND
            // matches the Type discriminator (UserAvatar=1, CharacterAvatar=2,
            // PostAttachment=3 in the UploadType enum).
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Uploads_TypedTarget",
                "(\"Type\" = 1 AND \"TargetUserId\" IS NOT NULL AND \"TargetCharacterId\" IS NULL AND \"TargetPostId\" IS NULL) OR " +
                "(\"Type\" = 2 AND \"TargetCharacterId\" IS NOT NULL AND \"TargetUserId\" IS NULL AND \"TargetPostId\" IS NULL) OR " +
                "(\"Type\" = 3 AND \"TargetPostId\" IS NOT NULL AND \"TargetUserId\" IS NULL AND \"TargetCharacterId\" IS NULL)"));
        });

        // Notepad scope lookup: entries/categories are always fetched for a
        // concrete notepad (container + type + owner for player notepads).
        modelBuilder.Entity<NotepadEntry>()
            .HasIndex(e => new { e.ContainerId, e.NotepadType, e.OwnerId });

        modelBuilder.Entity<NotepadCategory>()
            .HasIndex(c => new { c.ContainerId, c.NotepadType, c.OwnerId });

        // RoomAccess integrity: a grant targets exactly one of Character /
        // ReaderUser, and a room cannot hold duplicate grants for one target.
        modelBuilder.Entity<RoomAccess>(entity =>
        {
            // Explicit plain RoomId index: the composite indexes below are
            // partial, so the FK index is declared explicitly to keep it
            // from being dropped by the redundant-FK-index convention.
            entity.HasIndex(a => a.RoomId);

            entity.HasIndex(a => new { a.RoomId, a.CharacterId })
                .IsUnique()
                .HasFilter("\"CharacterId\" IS NOT NULL");

            entity.HasIndex(a => new { a.RoomId, a.ReaderUserId })
                .IsUnique()
                .HasFilter("\"ReaderUserId\" IS NOT NULL");

            // CHECK constraint: exactly one typed target column is non-null.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_RoomAccesses_TypedTarget",
                "(\"CharacterId\" IS NOT NULL AND \"ReaderUserId\" IS NULL) OR " +
                "(\"CharacterId\" IS NULL AND \"ReaderUserId\" IS NOT NULL)"));
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

    /// <summary>
    /// Website fundraising progress (single-row table, seeded)
    /// </summary>
    public DbSet<FundraisingGoal> FundraisingGoals { get; set; }

    /// <summary>
    /// Award type catalog (timeless, 6 rows in seed). Grant context (year, season,
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
    /// Achievement category catalog (13 rows in seed). SSOT for chain metadata
    /// (Title, Description, IconName, Metric, SortOrder, IsActive).
    /// </summary>
    public DbSet<AchievementCategory> AchievementCategories { get; set; }

    /// <summary>
    /// Achievement tier catalog (52 rows in seed). Each row belongs to a category;
    /// holds tier-specific data (Title, Threshold, Tier).
    /// </summary>
    public DbSet<AchievementType> AchievementTypes { get; set; }

    /// <summary>
    /// Achievement grants per user (auto-evaluated).
    /// </summary>
    public DbSet<UserAchievement> UserAchievements { get; set; }

    #endregion
}
