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

        if (isPostgres)
        {
            // Declared here rather than as raw SQL in the migration, for the same reason as
            // the seed: what the generator does not know about disappears when the migration
            // is regenerated. Installed for the substring predicates the search paths use —
            // a b-tree cannot serve ILIKE '%...%'. No index needs gin_trgm_ops yet; the first
            // one that does will find the extension already in place.
            modelBuilder.HasPostgresExtension("pg_trgm");
        }

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

        // Generated STORED tsvector columns + GIN indexes power every full-text
        // search in the system: messages and game posts (GET /v1/search/messages),
        // forum topics and comments (GET /v1/search/forum). The Post expression
        // strips [private=…]…[/private] blocks BEFORE indexing, so private text
        // is never tokenized and can never be matched or previewed by anyone.
        // regexp_replace, setweight and explicit-config to_tsvector are all
        // IMMUTABLE, so every expression here is valid inside a generated column.
        // Shadow "SearchVector" properties keep the tsvector off the
        // domain-facing entity surface.
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

            // A topic is title + body, and a title match means far more than a
            // body match — setweight is what lets ts_rank say so, instead of the
            // caller re-weighting after the fact.
            modelBuilder.Entity<Topic>(b =>
            {
                b.Property<NpgsqlTsVector>("SearchVector")
                    .HasComputedColumnSql(
                        "setweight(to_tsvector('russian', coalesce(\"Title\", '')), 'A') || " +
                        "setweight(to_tsvector('russian', coalesce(\"Text\", '')), 'B')",
                        stored: true);
                b.HasIndex("SearchVector").HasMethod("gin").HasDatabaseName("IX_Topics_SearchVector");
            });

            // Comment.EntityId is polymorphic, so this column indexes forum, game,
            // blog and publication comments alike: a generated column cannot look
            // at another table, and neither can a partial index. Restricting a
            // search to one kind of comment is therefore the query's job — the
            // join to Topics is what makes forum search forum-only.
            modelBuilder.Entity<Comment>(b =>
            {
                b.Property<NpgsqlTsVector>("SearchVector")
                    .HasComputedColumnSql("to_tsvector('russian', coalesce(\"Text\", ''))", stored: true);
                b.HasIndex("SearchVector").HasMethod("gin").HasDatabaseName("IX_Comments_SearchVector");
            });
        }

        // Composite index backing message cursor/keyset pagination and search
        // (filter by ChatId, order by CreatedUtc, tie-break by MessageId).
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.ChatId, m.CreatedUtc, m.MessageId });

        // Subscribers are always looked up as a pair: "who follows this board /
        // this game / this blog". Only SubscriberId was indexed — the reverse
        // direction, which is the one the profile, game and blog pages issue on
        // every render, scanned the whole table.
        modelBuilder.Entity<Subscription>()
            .HasIndex(s => new { s.TargetType, s.TargetId });

        // Likes are polymorphic and had no index at all. Every aggregate reads them
        // either as "likes of this entity" or as "likes of this kind" followed by a
        // join on EntityId, so EntityType leads: with the reverse order the join-shaped
        // reads (profile counters, community statistics) would not take the index at
        // all. The table grows without bound, so the scan degrades superlinearly.
        modelBuilder.Entity<Like>()
            .HasIndex(l => new { l.EntityType, l.EntityId });

        // The readable chat id is resolved by equality in GET /chats/{id}. Without
        // uniqueness a collision between an encoded serial and the reserved name of
        // the global chat would not fail - it would silently return whichever row
        // the plan happened to reach first.
        modelBuilder.Entity<Chat>()
            .HasIndex(c => c.PublicId)
            .IsUnique();

        // TopicNumber is the canonical topic URL key, and it is allocated as
        // MAX+1: two creates in the same board at the same time read the same
        // maximum. Without this constraint they both commit and one of the two
        // topics becomes unreachable by its own link, silently. Removed topics
        // keep their number — the URL of a deleted topic must stay a 410, not
        // start resolving to a newer one.
        modelBuilder.Entity<Topic>()
            .HasIndex(t => new { t.BoardId, t.TopicNumber })
            .IsUnique();

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

        // ---- Bootstrap seed ----
        // Declared here rather than written into the migration by hand. Seed the
        // migration does not know about is lost the moment the migration is
        // regenerated, which is the documented way to change the schema: the site
        // would come up with no boards, no tags, no system user and no global chat,
        // and nothing would say so. Declared in the model, EF emits it into every
        // migration it generates, and EnsureCreated and Migrate produce the same
        // database.
        //
        // Fixed identifiers from the zero family, so a repeated seed cannot create
        // duplicates and code outside migrations can reference a record by a
        // predictable id.

        modelBuilder.Entity<TagGroup>().HasData(
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Система",
                Description = "Ролевая система или набор правил, по которым ведется игра",
                SortOrder = 0
            },
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Жанр",
                Description = "Жанр и сеттинг игрового мира",
                SortOrder = 1
            },
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000003"),
                Title = "Формат игры",
                Description = "Тип игрового процесса и взаимодействия между участниками",
                SortOrder = 2
            },
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000004"),
                Title = "Формат постов",
                Description = "Стиль и объем игровых постов",
                SortOrder = 3
            },
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000005"),
                Title = "Темп",
                Description = "Ожидаемая скорость игры и частота постов",
                SortOrder = 4
            },
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000006"),
                Title = "Ограничения",
                Description = "Особые требования и ограничения для участников",
                SortOrder = 5
            },
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000007"),
                Title = "Новички",
                Description = "Игры от новичков и для новичков",
                SortOrder = 6
            },
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000008"),
                Title = "Деликатный контент",
                Description = "Контент, требующий осознанного согласия участников",
                SortOrder = 7
            });

        modelBuilder.Entity<Tag>().HasData(
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                ShortId = 1,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Black Bird Pie",
                Description = "Простая система с кубиком d6",
                SortOrder = 0
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                ShortId = 2,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "D&D",
                Description = "Dungeons & Dragons — все редакции классической ролевой системы",
                SortOrder = 1
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                ShortId = 3,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "D&D 5e",
                Description = "Dungeons & Dragons 5th Edition",
                SortOrder = 2
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                ShortId = 4,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "D100",
                Description = "Системы на основе процентного броска",
                SortOrder = 3
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                ShortId = 5,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Dawn of Worlds",
                Description = "Система для совместного создания мира",
                SortOrder = 4
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000006"),
                ShortId = 6,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Fallout",
                Description = "Адаптация сеттинга Fallout",
                SortOrder = 5
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000007"),
                ShortId = 7,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "FATAL",
                Description = "Без комментариев",
                SortOrder = 6
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                ShortId = 8,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Fate",
                Description = "Нарративная система с аспектами и фейт-пойнтами",
                SortOrder = 7
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000009"),
                ShortId = 9,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "FUDGE",
                Description = "Универсальный движок для реализации практически любого концепта",
                SortOrder = 8
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000a"),
                ShortId = 10,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "GURPS",
                Description = "Универсальная система на базе броска 3d6 vs Сложность",
                SortOrder = 9
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000b"),
                ShortId = 11,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Interlock",
                Description = "Система от R. Talsorian Games (Cyberpunk 2020 и другие)",
                SortOrder = 10
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000c"),
                ShortId = 12,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Microscope",
                Description = "Система для создания эпических историй",
                SortOrder = 11
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000d"),
                ShortId = 13,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Pathfinder 1e",
                Description = "Pathfinder первой редакции",
                SortOrder = 12
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000e"),
                ShortId = 14,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Pathfinder 2e",
                Description = "Pathfinder второй редакции",
                SortOrder = 13
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000f"),
                ShortId = 15,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "PbtA",
                Description = "Нарративные системы на базе 2d6 vs Сложность",
                SortOrder = 14
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000010"),
                ShortId = 16,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Risus",
                Description = "Минималистичная комедийная система",
                SortOrder = 15
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                ShortId = 17,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Savage Worlds",
                Description = "Легковесная универсальная система — Fast! Furious! Fun!",
                SortOrder = 16
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000012"),
                ShortId = 18,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Starfinder 1e",
                Description = "Sci-fi спин-офф Pathfinder",
                SortOrder = 17
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                ShortId = 19,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Starfinder 2e",
                Description = "Starfinder второй редакции",
                SortOrder = 18
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000014"),
                ShortId = 20,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Warhammer",
                Description = "Системы по вселенной Warhammer",
                SortOrder = 19
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000015"),
                ShortId = 21,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "World of Darkness",
                Description = "Мир Тьмы — вампиры, оборотни, маги",
                SortOrder = 20
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000016"),
                ShortId = 22,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Авторская",
                Description = "Оригинальная система от мастера игры",
                SortOrder = 21
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000017"),
                ShortId = 23,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Мафия",
                Description = "Психологическая детективная командная игра",
                SortOrder = 22
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000018"),
                ShortId = 24,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Словеска",
                Description = "Игра без формальной системы правил",
                SortOrder = 23
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000019"),
                ShortId = 25,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Эра Водолея",
                Description = "Отечественная система ролевых игр",
                SortOrder = 24
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000001a"),
                ShortId = 26,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Альтернативная история",
                Description = "Переосмысление исторических событий",
                SortOrder = 0
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000001b"),
                ShortId = 27,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Боевик",
                Description = "Акцент на экшн и сражениях",
                SortOrder = 1
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000001c"),
                ShortId = 28,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Детектив",
                Description = "Расследования и разгадывание тайн",
                SortOrder = 2
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000001d"),
                ShortId = 29,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Зомби",
                Description = "Зомби-апокалипсис и выживание",
                SortOrder = 3
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000001e"),
                ShortId = 30,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Историческое",
                Description = "Действие в реальную историческую эпоху",
                SortOrder = 4
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000001f"),
                ShortId = 31,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Киберпанк",
                Description = "Высокие технологии, низкий уровень жизни",
                SortOrder = 5
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000020"),
                ShortId = 32,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Комедия",
                Description = "Юмор и абсурдные ситуации",
                SortOrder = 6
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000021"),
                ShortId = 33,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Космоопера",
                Description = "Эпические приключения в космосе",
                SortOrder = 7
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000022"),
                ShortId = 34,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Мистика",
                Description = "Сверхъестественные элементы и тайны",
                SortOrder = 8
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000023"),
                ShortId = 35,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Наши дни",
                Description = "Современный реалистичный сеттинг",
                SortOrder = 9
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000024"),
                ShortId = 36,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Постапокалипсис",
                Description = "Мир после катастрофы",
                SortOrder = 10
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000025"),
                ShortId = 37,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Психоделика",
                Description = "Сюрреалистичные и необычные миры",
                SortOrder = 11
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000026"),
                ShortId = 38,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Стимпанк",
                Description = "Паровые технологии и викторианская эстетика",
                SortOrder = 12
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000027"),
                ShortId = 39,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Триллер",
                Description = "Напряжение и саспенс",
                SortOrder = 13
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000028"),
                ShortId = 40,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Трэш",
                Description = "Нарочито нелепый и провокационный контент",
                SortOrder = 14
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000029"),
                ShortId = 41,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Ужасы",
                Description = "Хоррор и атмосфера страха",
                SortOrder = 15
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000002a"),
                ShortId = 42,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Фантастика",
                Description = "Научная фантастика и будущее",
                SortOrder = 16
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000002b"),
                ShortId = 43,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Фэнтези",
                Description = "Магия, мечи и волшебные миры",
                SortOrder = 17
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000002c"),
                ShortId = 44,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000003"),
                Title = "Dungeon Crawl",
                Description = "Исследование подземелий и сражения с монстрами",
                SortOrder = 0
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000002d"),
                ShortId = 45,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000003"),
                Title = "PvP",
                Description = "Противостояние между игроками",
                SortOrder = 1
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000002e"),
                ShortId = 46,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000003"),
                Title = "Выживание",
                Description = "Борьба за выживание в суровых условиях",
                SortOrder = 2
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000002f"),
                ShortId = 47,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000003"),
                Title = "Песочница",
                Description = "Открытый мир без сюжетных ограничений",
                SortOrder = 3
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000030"),
                ShortId = 48,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000003"),
                Title = "Стратегия",
                Description = "Управление ресурсами и принятие глобальных решений",
                SortOrder = 4
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000031"),
                ShortId = 49,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000003"),
                Title = "Сюжетная",
                Description = "Фокус на развитии истории",
                SortOrder = 5
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000032"),
                ShortId = 50,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000003"),
                Title = "Тактика",
                Description = "Тактические бои и позиционирование",
                SortOrder = 6
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000033"),
                ShortId = 51,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000004"),
                Title = "Короткопост",
                Description = "Короткие посты в 1-3 абзаца",
                SortOrder = 0
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000034"),
                ShortId = 52,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000004"),
                Title = "Литературная",
                Description = "Развернутые литературные посты",
                SortOrder = 1
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000035"),
                ShortId = 53,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000005"),
                Title = "Неторопливый",
                Description = "Посты раз в несколько дней",
                SortOrder = 0
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000036"),
                ShortId = 54,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000005"),
                Title = "Скоростной",
                Description = "Несколько постов в день",
                SortOrder = 1
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000003e"),
                ShortId = 62,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000005"),
                Title = "Сухие сезоны",
                Description = "Возможны продолжительные периоды без постов",
                SortOrder = 2
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000037"),
                ShortId = 55,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000006"),
                Title = "Без мата",
                Description = "Нецензурная лексика запрещена",
                SortOrder = 0
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000038"),
                ShortId = 56,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000006"),
                Title = "Без насилия",
                Description = "Минимум жестокости и крови",
                SortOrder = 1
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000039"),
                ShortId = 57,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000006"),
                Title = "Grammar Nazi",
                Description = "Повышенные требования к грамотности",
                SortOrder = 2
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000003a"),
                ShortId = 58,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000006"),
                Title = "Для своих",
                Description = "Игра для знакомой компании",
                SortOrder = 3
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000003b"),
                ShortId = 59,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000006"),
                Title = "Обязателен мессенджер",
                Description = "Обсуждение игровых вопросов во внешнем мессенджере",
                SortOrder = 4
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000003c"),
                ShortId = 60,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000007"),
                Title = "Для новичков",
                Description = "Игра подходит для начинающих",
                SortOrder = 0
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000003d"),
                ShortId = 61,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000007"),
                Title = "Мастер-новичок",
                Description = "Мастер игры — начинающий",
                SortOrder = 1
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000003f"),
                ShortId = 63,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000008"),
                Title = "ERP",
                Description = "Erotic Role-Play: [tipimg:/images/erp-tooltip.gif]эротические сцены[/tipimg] как основа игрового процесса",
                SortOrder = 0
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000040"),
                ShortId = 64,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000008"),
                Title = "Шок-контент",
                Description = "Чернуха, максимально шокирующий и отталкивающий контент без ограничений",
                SortOrder = 1
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000041"),
                ShortId = 65,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000008"),
                Title = "Острые темы",
                Description = "Игра затрагивает спорные или чувствительные социальные темы",
                SortOrder = 2
            });

        modelBuilder.Entity<Board>().HasData(
            new Board
            {
                BoardId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Title = "Общий",
                Alias = "general",
                Description = "Жизнь сообщества и решения администрации",
                Order = 1,
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.RegularUser,
                TopicsCount = 1
            },
            new Board
            {
                BoardId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Title = "Игровые системы",
                Alias = "game-systems",
                Description = "Обсуждение правил и помощь в выборе системы",
                Order = 2,
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.RegularUser,
                TopicsCount = 0
            },
            new Board
            {
                BoardId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Title = "Поиск мастера и игроков",
                Alias = "looking-for-group",
                Description = "Набор игроков в игру или поиск мастера",
                Order = 3,
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.RegularUser,
                TopicsCount = 0
            },
            new Board
            {
                BoardId = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                Title = "Котел идей",
                Alias = "ideas",
                Description = "Обкатка задумок и поиск единомышленников",
                Order = 4,
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.RegularUser,
                TopicsCount = 0
            },
            new Board
            {
                BoardId = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                Title = "Конкурсы",
                Alias = "contests",
                Description = "Литературные и творческие состязания",
                Order = 5,
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.Moderator,
                TopicsCount = 0
            },
            new Board
            {
                BoardId = Guid.Parse("00000000-0000-0000-0000-000000000006"),
                Title = "Под столом",
                Alias = "off-topic",
                Description = "Музыка, книги, кино, мемы и все остальное",
                Order = 6,
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.RegularUser,
                TopicsCount = 0
            },
            new Board
            {
                BoardId = Guid.Parse("00000000-0000-0000-0000-000000000007"),
                Title = "Неролевые игры",
                Alias = "forum-games",
                Description = "Словесные игры, ассоциации и прочие развлечения",
                Order = 7,
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.RegularUser,
                TopicsCount = 0
            },
            new Board
            {
                BoardId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                Title = "Улучшение сайта",
                Alias = "improvements",
                Description = "Идеи и предложения по развитию сайта",
                Order = 8,
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.RegularUser,
                TopicsCount = 0
            },
            new Board
            {
                BoardId = Guid.Parse("00000000-0000-0000-0000-000000000009"),
                Title = "Ошибки",
                Alias = "bugs",
                Description = "Сообщения об ошибках на сайте",
                Order = 9,
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.RegularUser,
                TopicsCount = 0
            },
            new Board
            {
                BoardId = Guid.Parse("00000000-0000-0000-0000-00000000000a"),
                Title = "Для новичков",
                Alias = "newbies",
                Description = "Руководства, ответы на вопросы и помощь новичкам",
                Order = 10,
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.RegularUser,
                TopicsCount = 0
            },
            new Board
            {
                BoardId = Guid.Parse("00000000-0000-0000-0000-00000000000b"),
                Title = "Новости проекта",
                Alias = "news",
                Description = "Официальные новости, обновления и статистика",
                Order = 11,
                ViewPolicy = BoardAccessPolicy.Guest,
                CreateTopicPolicy = BoardAccessPolicy.Moderator,
                TopicsCount = 0
            });

        modelBuilder.Entity<AchievementCategory>().HasData(
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000001"),
                Code = "days_since_registration",
                Title = "Выслуга лет",
                Description = "Время с момента регистрации на сайте.",
                IconName = "hourglass",
                Metric = AchievementMetric.DaysSinceRegistration,
                SortOrder = 1,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000002"),
                Code = "game_posts_authored",
                Title = "Игровые посты",
                Description = "Игровые посты в активных играх. Считаются все, включая удаленные игры.",
                IconName = "scroll-quill",
                Metric = AchievementMetric.GamePostsAuthored,
                SortOrder = 2,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000003"),
                Code = "post_review_score_sum",
                Title = "Рейтинг",
                Description = "Сумма положительных оценок твоих игровых постов. Отрицательные оценки рейтинг не уменьшают.",
                IconName = "laurels",
                Metric = AchievementMetric.PostReviewScoreSum,
                SortOrder = 3,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000004"),
                Code = "games_hosted",
                Title = "Игры в роли ведущего",
                Description = "Игры, где ты мастер или ассистент.",
                IconName = "scepter",
                Metric = AchievementMetric.GamesHosted,
                SortOrder = 4,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000005"),
                Code = "games_played",
                Title = "Игры в роли игрока",
                Description = "Игры, где у тебя есть активный или бывший персонаж.",
                IconName = "sword",
                Metric = AchievementMetric.GamesPlayed,
                SortOrder = 5,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000006"),
                Code = "blogs_hosted",
                Title = "Блоги в роли ведущего",
                Description = "Блоги, где ты автор или ассистент.",
                IconName = "book",
                Metric = AchievementMetric.BlogsHosted,
                SortOrder = 6,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000007"),
                Code = "publications_authored",
                Title = "Публикации",
                Description = "Статьи в блогах. Черновики тоже считаются.",
                IconName = "papers",
                Metric = AchievementMetric.PublicationsAuthored,
                SortOrder = 7,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000008"),
                Code = "topics_authored",
                Title = "Топики",
                Description = "Форумные топики, которые ты создал.",
                IconName = "stabbed-note",
                Metric = AchievementMetric.TopicsAuthored,
                SortOrder = 8,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000009"),
                Code = "comments_authored",
                Title = "Комментарии",
                Description = "Все комментарии: форум, блоги, игры, публикации.",
                IconName = "discussion",
                Metric = AchievementMetric.CommentsAuthored,
                SortOrder = 9,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000a"),
                Code = "global_chat_messages",
                Title = "Глобальный чат",
                Description = "Сообщения в глобальном чате сайта.",
                IconName = "talk",
                Metric = AchievementMetric.GlobalChatMessages,
                SortOrder = 10,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000b"),
                Code = "likes_received",
                Title = "Лайки",
                Description = "Лайки на топиках, публикациях, комментариях и сообщениях чата. Игровые посты учитываются через \"Рейтинг\".",
                IconName = "heart-organ",
                Metric = AchievementMetric.LikesReceived,
                SortOrder = 11,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000c"),
                Code = "game_drops",
                Title = "Дропы",
                Description = "Игры, которые ты покинул добровольно. Смерть персонажа и изгнание мастером не считаются.",
                IconName = "walking-boot",
                Metric = AchievementMetric.GameDrops,
                SortOrder = 12,
                IsActive = true
            },
            new AchievementCategory
            {
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000d"),
                Code = "bans_received",
                Title = "Баны",
                Description = "Баны, полученные от модерации.",
                IconName = "plastic-duck",
                Metric = AchievementMetric.BansReceived,
                SortOrder = 13,
                IsActive = true
            });

        modelBuilder.Entity<AchievementType>().HasData(
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000005"),
                Code = "DAYS_366",
                Title = "Поселенец",
                Threshold = 366,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000001")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000006"),
                Code = "DAYS_1827",
                Title = "Старожил",
                Threshold = 1827,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000001")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000007"),
                Code = "DAYS_3653",
                Title = "Ветеран",
                Threshold = 3653,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000001")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000008"),
                Code = "DAYS_5479",
                Title = "Древний",
                Threshold = 5479,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000001")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000001"),
                Code = "POSTS_100",
                Title = "Простые начала",
                Threshold = 100,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000002")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000002"),
                Code = "POSTS_500",
                Title = "Продолжение следует",
                Threshold = 500,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000002")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000003"),
                Code = "POSTS_2000",
                Title = "Долгая партия",
                Threshold = 2000,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000002")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000004"),
                Code = "POSTS_5000",
                Title = "Приключение в жизнь",
                Threshold = 5000,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000002")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000009"),
                Code = "RATING_100",
                Title = "Подающий надежды",
                Threshold = 100,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000003")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000000a"),
                Code = "RATING_250",
                Title = "Видный талант",
                Threshold = 250,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000003")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000000b"),
                Code = "RATING_500",
                Title = "Опытный зубр",
                Threshold = 500,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000003")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000000c"),
                Code = "RATING_1000",
                Title = "Мастодонт-аксакал",
                Threshold = 1000,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000003")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000000d"),
                Code = "HOST_3",
                Title = "Подмастерье",
                Threshold = 3,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000004")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000000e"),
                Code = "HOST_10",
                Title = "Мастер",
                Threshold = 10,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000004")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000000f"),
                Code = "HOST_30",
                Title = "Грандмастер",
                Threshold = 30,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000004")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000010"),
                Code = "HOST_100",
                Title = "Архитектор миров",
                Threshold = 100,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000004")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000011"),
                Code = "PLAY_5",
                Title = "Искатель",
                Threshold = 5,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000005")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000012"),
                Code = "PLAY_20",
                Title = "Авантюрист",
                Threshold = 20,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000005")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000013"),
                Code = "PLAY_100",
                Title = "Герой",
                Threshold = 100,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000005")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000014"),
                Code = "PLAY_500",
                Title = "Легенда",
                Threshold = 500,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000005")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000015"),
                Code = "BLOGS_1",
                Title = "Свежий взгляд",
                Threshold = 1,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000006")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000016"),
                Code = "BLOGS_5",
                Title = "Небольшая подборка",
                Threshold = 5,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000006")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000017"),
                Code = "BLOGS_15",
                Title = "Именная коллекция",
                Threshold = 15,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000006")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000018"),
                Code = "BLOGS_50",
                Title = "Библиотека",
                Threshold = 50,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000006")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000002d"),
                Code = "PUBS_5",
                Title = "Проба пера",
                Threshold = 5,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000007")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000002e"),
                Code = "PUBS_25",
                Title = "Мысли вслух",
                Threshold = 25,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000007")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000002f"),
                Code = "PUBS_100",
                Title = "Постоянная рубрика",
                Threshold = 100,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000007")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000030"),
                Code = "PUBS_500",
                Title = "Без строчки ни дня",
                Threshold = 500,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000007")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000019"),
                Code = "TOPICS_5",
                Title = "Повод для обсуждения",
                Threshold = 5,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000008")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000001a"),
                Code = "TOPICS_25",
                Title = "Занятные темы",
                Threshold = 25,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000008")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000001b"),
                Code = "TOPICS_100",
                Title = "Дневная повестка",
                Threshold = 100,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000008")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000001c"),
                Code = "TOPICS_500",
                Title = "На целый раздел",
                Threshold = 500,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000008")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000001d"),
                Code = "COMMENTS_100",
                Title = "Свои пять копеек",
                Threshold = 100,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000009")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000001e"),
                Code = "COMMENTS_500",
                Title = "Живое участие",
                Threshold = 500,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000009")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000001f"),
                Code = "COMMENTS_2000",
                Title = "В гуще событий",
                Threshold = 2000,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000009")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000020"),
                Code = "COMMENTS_10000",
                Title = "Всегда есть что сказать",
                Threshold = 10000,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-000000000009")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000021"),
                Code = "CHAT_500",
                Title = "Прохожий",
                Threshold = 500,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000a")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000022"),
                Code = "CHAT_5000",
                Title = "Свой человек",
                Threshold = 5000,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000a")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000023"),
                Code = "CHAT_25000",
                Title = "Чат-завсегдатай",
                Threshold = 25000,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000a")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000024"),
                Code = "CHAT_100000",
                Title = "Вечный онлайн",
                Threshold = 100000,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000a")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000031"),
                Code = "LIKES_25",
                Title = "В узких кругах",
                Threshold = 25,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000b")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000032"),
                Code = "LIKES_100",
                Title = "Душа компании",
                Threshold = 100,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000b")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000033"),
                Code = "LIKES_500",
                Title = "Народный любимец",
                Threshold = 500,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000b")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000034"),
                Code = "LIKES_2000",
                Title = "Первый в сердцах",
                Threshold = 2000,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000b")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000029"),
                Code = "DROPS_1",
                Title = "Перекати-поле",
                Threshold = 1,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000c")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000002a"),
                Code = "DROPS_3",
                Title = "Беглец",
                Threshold = 3,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000c")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000002b"),
                Code = "DROPS_10",
                Title = "Дезертир",
                Threshold = 10,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000c")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-00000000002c"),
                Code = "DROPS_30",
                Title = "Пропавший без вести",
                Threshold = 30,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000c")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000025"),
                Code = "BANS_1",
                Title = "Яйцо с характером",
                Threshold = 1,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000d")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000026"),
                Code = "BANS_3",
                Title = "Выпавший из гнезда",
                Threshold = 3,
                Tier = 2,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000d")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000027"),
                Code = "BANS_10",
                Title = "Утенок-террорист",
                Threshold = 10,
                Tier = 3,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000d")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000028"),
                Code = "BANS_30",
                Title = "Селезень-Рецидивист",
                Threshold = 30,
                Tier = 4,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000d")
            });

        modelBuilder.Entity<AwardType>().HasData(
            new AwardType
            {
                AwardTypeId = new Guid("00000000-0000-0000-0001-000000000001"),
                Code = "contest_first",
                Title = "Литконкурс",
                Description = "Победитель конкурса",
                IconName = "trophy-cup",
                Tier = 1,
                SortOrder = 1,
                IsActive = true
            },
            new AwardType
            {
                AwardTypeId = new Guid("00000000-0000-0000-0001-000000000002"),
                Code = "contest_second",
                Title = "Литконкурс",
                Description = "Серебряный призер конкурса",
                IconName = "trophy-cup",
                Tier = 2,
                SortOrder = 2,
                IsActive = true
            },
            new AwardType
            {
                AwardTypeId = new Guid("00000000-0000-0000-0001-000000000003"),
                Code = "contest_third",
                Title = "Литконкурс",
                Description = "Бронзовый призер конкурса",
                IconName = "trophy-cup",
                Tier = 3,
                SortOrder = 3,
                IsActive = true
            },
            new AwardType
            {
                AwardTypeId = new Guid("00000000-0000-0000-0001-000000000004"),
                Code = "popular_vote",
                Title = "Народное признание, например",
                Description = "Лучшая работа конкурса по голосованию участников",
                IconName = "ribbon-medal",
                Tier = 1,
                SortOrder = 4,
                IsActive = true
            },
            new AwardType
            {
                AwardTypeId = new Guid("00000000-0000-0000-0001-000000000005"),
                Code = "best_critic",
                Title = "Лучший критик",
                Description = "Лучшие рецензии сезона по решению жюри",
                IconName = "quill-ink",
                Tier = 1,
                SortOrder = 5,
                IsActive = true
            },
            new AwardType
            {
                AwardTypeId = new Guid("00000000-0000-0000-0001-000000000006"),
                Code = "guesser",
                Title = "Угадайка",
                Description = "Угадал больше всех авторов конкурсных работ",
                IconName = "magnifying-glass",
                Tier = 1,
                SortOrder = 6,
                IsActive = true
            },
            new AwardType
            {
                AwardTypeId = new Guid("00000000-0000-0000-0001-000000000007"),
                Code = "honorary_goblin",
                Title = "Почетный гоблин",
                Description = "Бывший гоблин, отдавший сообществу годы службы",
                IconName = "goblin",
                Tier = 5,
                SortOrder = 7,
                IsActive = true
            });

        modelBuilder.Entity<ContestSeries>().HasData(
            new ContestSeries
            {
                ContestSeriesId = new Guid("00000000-0000-0000-0004-000000000001"),
                ContestType = ContestType.Literary,
                Number = 23,
                Year = 2024,
                TopicUrl = "https://dm.am/forum/topic/contest-results-lit-23",
                IsActive = true
            },
            new ContestSeries
            {
                ContestSeriesId = new Guid("00000000-0000-0000-0004-000000000002"),
                ContestType = ContestType.Literary,
                Number = 22,
                Year = 2023,
                TopicUrl = "https://dm.am/forum/topic/contest-results-lit-22",
                IsActive = true
            },
            new ContestSeries
            {
                ContestSeriesId = new Guid("00000000-0000-0000-0004-000000000003"),
                ContestType = ContestType.Literary,
                Number = 21,
                Year = 2023,
                TopicUrl = "https://dm.am/forum/topic/contest-results-lit-21",
                IsActive = true
            },
            new ContestSeries
            {
                ContestSeriesId = new Guid("00000000-0000-0000-0004-000000000004"),
                ContestType = ContestType.Literary,
                Number = 20,
                Year = 2022,
                TopicUrl = "https://dm.am/forum/topic/contest-results-lit-20",
                IsActive = true
            },
            new ContestSeries
            {
                ContestSeriesId = new Guid("00000000-0000-0000-0004-000000000005"),
                ContestType = ContestType.Art,
                Number = 2,
                Year = 2024,
                TopicUrl = "https://dm.am/forum/topic/contest-results-art-2",
                IsActive = true
            },
            new ContestSeries
            {
                ContestSeriesId = new Guid("00000000-0000-0000-0004-000000000006"),
                ContestType = ContestType.Art,
                Number = 1,
                Year = 2023,
                TopicUrl = "https://dm.am/forum/topic/contest-results-art-1",
                IsActive = true
            });

        // The system author every automated action is attributed to. Cannot log in:
        // no salt, no hash.
        modelBuilder.Entity<User>().HasData(
            new User
            {
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Username = "Робот-Администратор",
                Email = "system@dm.local",
                CreatedUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Role = UserRole.System,
                AccessPolicy = AccessPolicy.NotSpecified,
                Salt = "",
                PasswordHash = "",
                PasswordHashVersion = 0,
                RatingDisabled = true,
                QualityRating = 0,
                QuantityRating = 0,
                IsRemoved = false,
                Gender = Gender.Unknown,
                ShowBirthday = false
            });

        modelBuilder.Entity<Chat>().HasData(
            new Chat
            {
                ChatId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Type = ChatType.Global,
                Title = "Глобальный чат",
                // Every other chat gets its readable id encoded from the serial the
                // database assigns on insert; a seeded row never goes through that
                // path, so the one chat that is addressable by name gets it here.
                PublicId = "global"
            });

        modelBuilder.Entity<Topic>().HasData(
            new Topic
            {
                TopicId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                BoardId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                TopicNumber = 1,
                AuthorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                CreatedUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Title = "Отзывы о ДМ",
                Text = "Ваши отзывы отсюда попадают (после минимального анализа на нарушения правил) прямиком на главную.",
                IsAttached = true,
                IsClosed = false,
                IsRemoved = false
            },
            new Topic
            {
                TopicId = Guid.Parse("00000000-0000-0000-0000-000000000100"),
                BoardId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                TopicNumber = 2,
                AuthorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                // One second later, so "the newest topic" is unambiguous
                CreatedUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 1, TimeSpan.Zero),
                Title = "Обсуждение действий администрации",
                Text = "Здесь можно обсудить решения модераторов и администрации. Конструктивная критика приветствуется.",
                IsAttached = true,
                IsClosed = false,
                IsRemoved = false
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
        // block must be removed manually (a comment marks the spot in InitialCreate.cs).
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

        // Username and email are looked up case-insensitively everywhere
        // (lower(column) = lower(value)), so the indexes that serve those
        // lookups are expression indexes on lower(...). EF cannot express an
        // index over an expression, so they are created by raw SQL in the
        // migration — see the IX_Users_*_Lower statements there. Declaring a
        // plain HasIndex here instead would build a b-tree over the raw column
        // that no case-insensitive predicate can use, which is exactly the
        // state this replaced: the name said Lower, the index did not.

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

        // Notepad scope lookup: entries are always fetched for a concrete
        // notepad (container + type + owner for player notepads).
        modelBuilder.Entity<NotepadEntry>()
            .HasIndex(e => new { e.ContainerId, e.NotepadType, e.OwnerId });

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
