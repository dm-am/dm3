using System.Linq;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Persistence.Repositories.Search;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq.Expressions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using SystemUser = DM.Domain.Core.Identity.SystemUser;
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
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace DM.Infrastructure.Persistence;

/// <summary>
/// RDB storage context
/// </summary>
public class DmDbContext : DbContext
{
    /// <inheritdoc />
    /// <summary>
    /// The bodies whose visible text is projected beside them, and where each one
    /// keeps that body.
    /// </summary>
    /// <remarks>
    /// A table rather than an interface on the entities: what makes a property the
    /// searchable body of a row is what it means, and the surface it is displayed
    /// on is a fact about the feature rather than about the type. [private] is a
    /// node of the tree only on the surfaces that declare it, so a projection made
    /// on the wrong one leaves the block a literal and puts its contents into the
    /// index and into every preview.
    /// </remarks>
    private static readonly (Type Entity, string Body, BbSurface Surface)[] ProjectedBodies =
    [
        (typeof(Message), nameof(Message.Text), BbSurface.GlobalChatMessage),
        (typeof(Post), nameof(Post.GameText), BbSurface.GamePost),
        (typeof(Topic), nameof(Topic.Text), BbSurface.Comment),
        (typeof(Comment), nameof(Comment.Text), BbSurface.Comment),
    ];

    /// <summary>
    /// Refreshes the projected text of everything about to be written.
    /// </summary>
    /// <remarks>
    /// Here rather than in an interceptor because the context is constructed in
    /// five places - three hosts and two test factories - and an interceptor
    /// forgotten in one of them leaves that path writing rows whose projection is
    /// stale or empty, which nothing fails on and which shows up as a body that
    /// cannot be found by its own words.
    /// </remarks>
    private void ProjectSearchText()
    {
        foreach (var (type, body, surface) in ProjectedBodies)
        {
            foreach (var entry in ChangeTracker.Entries().Where(e => e.Entity.GetType() == type))
            {
                if (entry.State != EntityState.Added &&
                    !(entry.State == EntityState.Modified && entry.Property(body).IsModified))
                {
                    continue;
                }

                entry.Property("SearchText").CurrentValue =
                    SearchTextProjection.Of(entry.Property(body).CurrentValue as string, surface);
            }
        }
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProjectSearchText();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProjectSearchText();
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
    /// OnModelCreating and for TagNumbers, which reads it.
    /// </summary>
    internal const string TagShortIdSequence = "TagShortIds";

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

        #region Assistant Indexes

        // A person assists a game or a blog once. The row is written when an
        // invitation is redeemed, and redeeming it twice - two accepts of the same
        // live link arriving together - passed the "already an assistant" read on
        // both sides and inserted the person twice. Nothing downstream dedupes:
        // the assistant list showed the name twice and removing them left one row
        // behind, still holding the rights.
        modelBuilder.Entity<GameAssistant>()
            .HasIndex(a => new { a.GameId, a.UserId })
            .IsUnique();

        modelBuilder.Entity<BlogAssistant>()
            .HasIndex(a => new { a.BlogId, a.UserId })
            .IsUnique();

        #endregion

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
                Title = "Хостинг и домен на год",
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

        // One spelling of the expression, because four copies of it is four places
        // for the configuration to drift and nothing compiles any of them. The
        // dictionary itself is named beside the repositories that query these
        // columns: it has to agree across the two, and nothing here can see them.
        static string SearchVectorSql(string column) =>
            $"to_tsvector('{SearchTextConfiguration.Name}', regexp_replace(coalesce(\"{column}\", ''), " +
            $"'{PrivateBlockMarkup.BlockPattern}', ' ', 'gi'))";


        // Generated STORED tsvector columns + GIN indexes power every full-text
        // search in the system: messages and game posts (GET /v1/search/messages),
        // forum topics and comments (GET /v1/search/forum).
        //
        // All four read SearchText - the visible text of the body, written beside
        // it by SearchTextProjection. Read off the raw body instead, the index
        // tokenised markup: a tag name was a word one could search for, and the
        // preview built from the same string showed a reader fragments of BBCode.
        // The regexp below strips [private=…]…[/private] before indexing and is a
        // second lock over the projection, which already removes it: the column
        // cannot fail, and a process can.
        //
        // regexp_replace, setweight and explicit-config to_tsvector are all
        // IMMUTABLE, so every expression here is valid inside a generated column.
        // Shadow "SearchVector" properties keep the tsvector off the
        // domain-facing entity surface.
        // The visible text of every indexed body, kept beside it. Declared for all
        // providers and not only for Postgres: the in-memory store used by the unit
        // tier has to carry the same shape, and a column that exists in one and not
        // the other is a difference the tests cannot see.
        modelBuilder.Entity<Message>().Property<string>("SearchText").HasDefaultValue(string.Empty);
        modelBuilder.Entity<Post>().Property<string>("SearchText").HasDefaultValue(string.Empty);
        modelBuilder.Entity<Topic>().Property<string>("SearchText").HasDefaultValue(string.Empty);
        modelBuilder.Entity<Comment>().Property<string>("SearchText").HasDefaultValue(string.Empty);

        if (isPostgres)
        {
            modelBuilder.Entity<Message>(b =>
            {
                b.Property<NpgsqlTsVector>("SearchVector")
                    .HasComputedColumnSql(SearchVectorSql("SearchText"), stored: true);
                b.HasIndex("SearchVector").HasMethod("gin");
            });

            modelBuilder.Entity<Post>(b =>
            {
                // The pattern is the shared one, not a copy of it: the same string
                // cuts a block out of a snippet in .NET, and a second spelling here
                // means indexing text no preview shows — or hiding text the index
                // holds.
                //
                // Cut unconditionally, although two room settings can open a private
                // block to everyone who reads the room. A generated column sees its
                // own row and nothing else, so honouring them would mean either a
                // denormalised copy of the setting on every post - stale the moment
                // the setting changes, and stale in the direction that leaves
                // private text indexed - or a rewrite of every post in the room
                // inside the transaction that changes it. The index stays strictly
                // narrower than the page: never wider.
                b.Property<NpgsqlTsVector>("SearchVector")
                    .HasComputedColumnSql(SearchVectorSql("SearchText"), stored: true);
                b.HasIndex("SearchVector").HasMethod("gin").HasDatabaseName("IX_Posts_SearchVector");
            });

            // A topic is title + body, and a title match means far more than a
            // body match — setweight is what lets ts_rank say so, instead of the
            // caller re-weighting after the fact.
            modelBuilder.Entity<Topic>(b =>
            {
                b.Property<NpgsqlTsVector>("SearchVector")
                    .HasComputedColumnSql(
                        $"setweight(to_tsvector('{SearchTextConfiguration.Name}', coalesce(\"Title\", '')), 'A') || " +
                        $"setweight({SearchVectorSql("SearchText")}, 'B')",
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
                    .HasComputedColumnSql(SearchVectorSql("SearchText"), stored: true);
                b.HasIndex("SearchVector").HasMethod("gin").HasDatabaseName("IX_Comments_SearchVector");
            });
        }

        // Composite index backing message cursor/keyset pagination and search
        // (filter by ChatId, order by CreatedUtc, tie-break by MessageId).
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.ChatId, m.CreatedUtc, m.MessageId });

        // A page of a room's posts and a page of a discussion's comments are read in one
        // shape: filter by the parent, order by CreatedUtc, tie-break by the identifier
        // (PostRepository.Get; CommentSorting, whose every branch ends on CommentId).
        // Both tables were reached by an index on the parent alone, so answering for one
        // page of a long room or a long discussion meant reading and sorting all of it,
        // and the cost grew with the room rather than with the page. The import of DM2 is
        // what makes a room of that size the normal case rather than the extreme one.
        //
        // For comments this is the creation-ordered ascending branch, the default one. The
        // descending branch orders CreatedUtc down and CommentId up, so it takes the
        // parent's range and the timestamps from the index read backwards and re-sorts
        // inside one timestamp; the "likes" branch orders by a correlated count and takes
        // nothing from the index beyond that range.
        //
        // Each composite also replaces the conventional index on the foreign key alone
        // (IX_Posts_RoomId, IX_Comments_EntityId): the parent is the leading column, so
        // every lookup by parent keeps an index — the same trade the message composite
        // above already makes.
        //
        // Unfiltered, like that composite and unlike the partial statistics indexes above.
        // The soft-delete predicate is in every one of these reads — the global query
        // filter puts it there — so a partial index would be usable, but it would be usable
        // only there, and it is replacing an index that answers a lookup by parent
        // whichever rows the caller asked for.
        modelBuilder.Entity<Post>()
            .HasIndex(p => new { p.RoomId, p.CreatedUtc, p.PostId });

        modelBuilder.Entity<Comment>()
            .HasIndex(c => new { c.EntityId, c.CreatedUtc, c.CommentId });

        // Subscribers are always looked up as a pair: "who follows this board /
        // this game / this blog". Only SubscriberId was indexed — the reverse
        // direction, which is the one the profile, game and blog pages issue on
        // every render, scanned the whole table.
        modelBuilder.Entity<Subscription>()
            .HasIndex(s => new { s.TargetType, s.TargetId });

        // One subscription per (subscriber, target). All three subscribe services
        // check first and insert after, so two overlapping requests both find
        // nothing and both insert, and the extra row is not something the reader
        // can clear: the button reads "subscribed" while any row is left, and each
        // unsubscribe click removes one. Leads with SubscriberId, so it replaces
        // the plain FK index the convention used to add rather than adding to it.
        modelBuilder.Entity<Subscription>()
            .HasIndex(s => new { s.SubscriberId, s.TargetType, s.TargetId })
            .IsUnique();

        // One live like per (entity, reader). Liking is check-then-insert over the
        // loaded navigation, so two overlapping clicks both find nothing and both
        // insert: the counter then reports one person twice, and the same duplicate
        // reaches the LikesReceived metric, where somebody else's double click moves
        // the author towards an award. Partial on live rows because unliking sets
        // IsRemoved and liking again has to be allowed — the same shape as the
        // endorsement and review indexes above.
        //
        // EntityType leads because every aggregate reads likes either as "likes of
        // this entity" or as "likes of this kind" joined on EntityId: equality on the
        // type gives a range already ordered by EntityId, which is what the join-shaped
        // reads (profile counters, community statistics) want. The reverse order is
        // usable too — a nested loop driven by EntityId would take it — so the choice
        // is about the shape and the cost of the join, not about the index being read
        // at all. Replaces the plain (EntityType, EntityId) index: it is the leading
        // pair, and no read of this table looks at removed rows.
        var likeIndexBuilder = modelBuilder.Entity<Like>()
            .HasIndex(l => new { l.EntityType, l.EntityId, l.UserId });
        if (isPostgres)
        {
            likeIndexBuilder.HasFilter("\"IsRemoved\" = false");
        }
        likeIndexBuilder.IsUnique();

        // A game carries a tag once. The link rows are written from a resolved
        // catalogue today, which is why no duplicate exists to clean up, but the
        // required-tag filter counts rows and not distinct tags, so a second row for
        // one tag would answer a search for "D&D AND detective" with a game that only
        // has D&D twice. Leading with GameId, so it replaces the conventional FK index
        // rather than adding to it.
        modelBuilder.Entity<GameTag>()
            .HasIndex(t => new { t.GameId, t.TagId })
            .IsUnique();

        // The same rule one level up: two tags may not carry one number. ShortId is the
        // tag's public key — a /games link is written in it, and the required, optional and
        // excluded predicates compare by it — and it used to be handed out as MAX + 1 over
        // the table, so two creates in one moment read one maximum and both committed it.
        // The required-tag filter counts matching rows rather than distinct tags, so a game
        // carrying both halves of a shared number answers a two-tag search while holding
        // one of the two. The number comes from the sequence below now; this refuses a
        // duplicate that arrives some other way.
        modelBuilder.Entity<Tag>()
            .HasIndex(t => t.ShortId)
            .IsUnique();

        // The sequence those numbers are drawn from, read by TagNumbers. Declared on the
        // model rather than written into the migration by hand, for the reason the
        // bootstrap seed below is: regenerating the migration is the documented way to
        // change the schema, and a database object the model does not know about does not
        // survive that. Postgres only, like the partial indexes above — the value is taken
        // with nextval, which no other provider used here has.
        //
        // Starts one past the highest number the seeded catalogue ships with, so nothing it
        // ships is handed out a second time. A tag appended to that catalogue has to move
        // this start with it; if that is forgotten, the unique index above turns the
        // overlap into a refused insert rather than two tags holding one number.
        if (isPostgres)
        {
            modelBuilder.HasSequence<int>(TagShortIdSequence).StartsAt(73);
        }

        // The readable chat id is resolved by equality in GET /chats/{id}. Without
        // uniqueness a collision between an encoded serial and the reserved name of
        // the global chat would not fail - it would silently return whichever row
        // the plan happened to reach first.
        modelBuilder.Entity<Chat>()
            .HasIndex(c => c.PublicId)
            .IsUnique();

        // Games and blogs resolve their pages by the same readable key and by the same
        // equality, so they get the same constraint — and the index besides, which
        // neither of them had: the address in every link on the site was answered by a
        // sequential scan.
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.PublicId)
            .IsUnique();

        modelBuilder.Entity<Blog>()
            .HasIndex(b => b.PublicId)
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

        // RoomNumber is the same key one level down: a room is addressed as
        // /games/{game}/rooms/{number}, and the number is what the address
        // resolves by. It is allocated as MAX+1 within the game, so two rooms
        // created at once read the same maximum, and two rooms sharing a number
        // means one of them can never be opened by its own link. A removed room
        // keeps its number for the reason a removed topic does. The pair also
        // replaces the plain GameId index: every room read filters by the game
        // first, so the composite serves those reads unchanged.
        modelBuilder.Entity<Room>()
            .HasIndex(r => new { r.GameId, r.RoomNumber })
            .IsUnique();

        // Off by default: a closed room is named to everybody unless its master
        // says otherwise, and a row written without the column has to mean that.
        modelBuilder.Entity<Room>()
            .Property(r => r.HiddenWithoutAccess)
            .HasDefaultValue(false);

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
        // migration it generates, so the seed travels with whichever migration is
        // current. The two ways to build the schema are not interchangeable:
        // EnsureCreated materialises the model as it stands, including the
        // polymorphic foreign keys that are cut out of the migration by hand, so
        // only Migrate produces the schema the code expects.
        //
        // Fixed identifiers from the zero family, so a repeated seed cannot create
        // duplicates and code outside migrations can reference a record by a
        // predictable id.

        // MaxTagsPerGame is left out of a group that limits nothing: the column is
        // nullable and null is the absence of a limit, not a value forgotten. The
        // groups that carry one are the ones where a game answering "both" says
        // less than a game answering one thing.
        modelBuilder.Entity<TagGroup>().HasData(
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Система",
                Description = "Ролевая система или набор правил, по которым ведется игра",
                SortOrder = 0,
                MaxTagsPerGame = 2
            },
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Title = "Жанр",
                Description = "Жанр и сеттинг игрового мира",
                SortOrder = 1,
                MaxTagsPerGame = 3
            },
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000003"),
                Title = "Формат игры",
                Description = "Тип игрового процесса и взаимодействия между участниками",
                SortOrder = 2,
                MaxTagsPerGame = 2
            },
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000004"),
                Title = "Формат постов",
                Description = "Стиль и объем игровых постов",
                SortOrder = 3,
                MaxTagsPerGame = 1
            },
            new TagGroup
            {
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000005"),
                Title = "Темп",
                Description = "Ожидаемая скорость игры и частота постов",
                SortOrder = 4,
                MaxTagsPerGame = 1
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
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000042"),
                ShortId = 66,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "2d20",
                Description = "Движок Modiphius: пул из двух d20 против характеристики",
                SortOrder = 0
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000043"),
                ShortId = 67,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "AD&D",
                Description = "Первая и вторая редакции Dungeons & Dragons",
                SortOrder = 1
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                ShortId = 1,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Black Bird Pie",
                Description = "Простая система с кубиком d6",
                SortOrder = 2
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000044"),
                ShortId = 68,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Cypher",
                Description = "Движок Monte Cook Games: сложность против броска d20",
                SortOrder = 3
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                ShortId = 2,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "D&D",
                Description = "Семейство Dungeons & Dragons",
                SortOrder = 4
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000045"),
                ShortId = 69,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "D&D 3.5",
                Description = "Третья редакция Dungeons & Dragons, включая 3.0",
                SortOrder = 5
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000046"),
                ShortId = 70,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "D&D 4e",
                Description = "Четвертая редакция Dungeons & Dragons",
                SortOrder = 6
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                ShortId = 3,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "D&D 5e",
                Description = "Пятая редакция Dungeons & Dragons",
                SortOrder = 7
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                ShortId = 4,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "d100",
                Description = "Системы на основе процентного броска",
                SortOrder = 8
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                ShortId = 5,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Dawn of Worlds",
                Description = "Система для совместного создания мира",
                SortOrder = 9
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000006"),
                ShortId = 6,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Fallout",
                Description = "Адаптация сеттинга Fallout",
                SortOrder = 10
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000007"),
                ShortId = 7,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "FATAL",
                Description = "Без комментариев",
                SortOrder = 11
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                ShortId = 8,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Fate",
                Description = "Нарративная система с аспектами и фейт-пойнтами",
                SortOrder = 12
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000047"),
                ShortId = 71,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "FitD",
                Description = "Forged in the Dark: системы на движке Blades in the Dark",
                SortOrder = 13
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000009"),
                ShortId = 9,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "FUDGE",
                Description = "Универсальный движок для реализации практически любого концепта",
                SortOrder = 14
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000048"),
                ShortId = 72,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "GUMSHOE",
                Description = "Детективный движок Robin Laws: улики не пропускаются на броске",
                SortOrder = 15
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000a"),
                ShortId = 10,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "GURPS",
                Description = "Универсальная система на базе броска 3d6 vs Сложность",
                SortOrder = 16
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000b"),
                ShortId = 11,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Interlock",
                Description = "Система от R. Talsorian Games (Cyberpunk 2020 и другие)",
                SortOrder = 17
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000c"),
                ShortId = 12,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Microscope",
                Description = "Система для создания эпических историй",
                SortOrder = 18
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000d"),
                ShortId = 13,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Pathfinder 1e",
                Description = "Pathfinder первой редакции",
                SortOrder = 19
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000e"),
                ShortId = 14,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Pathfinder 2e",
                Description = "Pathfinder второй редакции",
                SortOrder = 20
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-00000000000f"),
                ShortId = 15,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "PbtA",
                Description = "Нарративные системы на базе 2d6 vs Сложность",
                SortOrder = 21
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000010"),
                ShortId = 16,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Risus",
                Description = "Минималистичная комедийная система",
                SortOrder = 22
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                ShortId = 17,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Savage Worlds",
                Description = "Легковесная универсальная система — Fast! Furious! Fun!",
                SortOrder = 23
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000012"),
                ShortId = 18,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Starfinder 1e",
                Description = "Sci-fi спин-офф Pathfinder",
                SortOrder = 24
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                ShortId = 19,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Starfinder 2e",
                Description = "Starfinder второй редакции",
                SortOrder = 25
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000014"),
                ShortId = 20,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Warhammer",
                Description = "Системы по вселенной Warhammer",
                SortOrder = 26
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000015"),
                ShortId = 21,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "World of Darkness",
                Description = "Мир Тьмы: Storyteller, Storytelling и Chronicles of Darkness",
                SortOrder = 27
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000016"),
                ShortId = 22,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Авторская",
                Description = "Оригинальная система от мастера игры",
                SortOrder = 28
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000017"),
                ShortId = 23,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Мафия",
                Description = "Психологическая детективная командная игра",
                SortOrder = 29
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000018"),
                ShortId = 24,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Словеска",
                Description = "Игра без формальной системы правил",
                SortOrder = 30
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000019"),
                ShortId = 25,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "Эра Водолея",
                Description = "Отечественная система ролевых игр",
                SortOrder = 31
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
                SortOrder = 1
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000036"),
                ShortId = 54,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000005"),
                Title = "Скоростной",
                Description = "Несколько постов в день",
                SortOrder = 0
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
                SortOrder = 1
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000038"),
                ShortId = 56,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000006"),
                Title = "Без насилия",
                Description = "Минимум жестокости и крови",
                SortOrder = 2
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000039"),
                ShortId = 57,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000006"),
                Title = "Grammar Nazi",
                Description = "Повышенные требования к грамотности",
                SortOrder = 0
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
                SortOrder = 2
            },
            new Tag
            {
                TagId = Guid.Parse("00000000-0000-0000-0000-000000000041"),
                ShortId = 65,
                TagGroupId = Guid.Parse("00000000-0000-0000-0005-000000000008"),
                Title = "Острые темы",
                Description = "Игра затрагивает спорные или чувствительные социальные темы",
                SortOrder = 1
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
                // Two topics are seeded into this board below. The column is a
                // denormalised count that only a topic write recomputes, so a seeded
                // value that disagrees with the seeded rows is what a freshly migrated
                // database shows the forum — and it also becomes the unread count for
                // everyone who has never opened the board.
                TopicsCount = 2,
                // The last-topic block is denormalised the same way and was left unset,
                // so a board with two topics showed an empty "last activity" column
                // until somebody created or deleted a topic in it. Points at the newer
                // of the two seeded topics.
                LastTopicId = Guid.Parse("00000000-0000-0000-0000-000000000100"),
                LastTopicNumber = 2,
                LastTopicTitle = "Обсуждение действий администрации",
                LastTopicAuthorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                LastTopicCreatedUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 1, TimeSpan.Zero)
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
                Description = "Время с момента регистрации на сайте",
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
                Description = "Игровые посты во всех играх, включая удаленные",
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
                Description = "Сумма оценок игровых постов с учетом минусов",
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
                Description = "Игры в роли мастера или ассистента",
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
                Description = "Игры с активным или бывшим персонажем",
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
                Description = "Блоги в роли автора или ассистента",
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
                Description = "Статьи в блогах, включая черновики",
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
                Description = "Топики, созданные на форуме",
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
                Description = "Комментарии на форуме, в блогах, играх и публикациях",
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
                Description = "Сообщения в глобальном чате",
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
                Description = "Лайки на топиках, публикациях, комментариях и сообщениях чата",
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
                Description = "Игры, покинутые добровольно (смерть персонажа и изгнание мастером не считаются)",
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
                Description = "Баны, полученные от модерации",
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
                Title = "Самобытный талант",
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
                Title = "Мастодонт ремесла",
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
                Title = "Покинувший строй",
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
                Title = "Выпавший из гнезда",
                Threshold = 1,
                Tier = 1,
                AchievementCategoryId = new Guid("00000000-0000-0000-0003-00000000000d")
            },
            new AchievementType
            {
                AchievementTypeId = new Guid("00000000-0000-0000-0002-000000000026"),
                Code = "BANS_3",
                Title = "Крякнувший лишнего",
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
                Description = "Первое место в конкурсе",
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
                Description = "Второе место в конкурсе",
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
                Description = "Третье место в конкурсе",
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
                Description = "Лучшая работа по голосованию участников",
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
                Description = "Лучшие рецензии конкурса по решению жюри",
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
                Description = "Больше всех угаданных авторов конкурсных работ",
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
                Description = "Годы службы сообществу в команде гоблинов",
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
                TopicUrl = "/forum/topic/contest-results-lit-23",
                IsActive = true
            },
            new ContestSeries
            {
                ContestSeriesId = new Guid("00000000-0000-0000-0004-000000000002"),
                ContestType = ContestType.Literary,
                Number = 22,
                Year = 2023,
                TopicUrl = "/forum/topic/contest-results-lit-22",
                IsActive = true
            },
            new ContestSeries
            {
                ContestSeriesId = new Guid("00000000-0000-0000-0004-000000000003"),
                ContestType = ContestType.Literary,
                Number = 21,
                Year = 2023,
                TopicUrl = "/forum/topic/contest-results-lit-21",
                IsActive = true
            },
            new ContestSeries
            {
                ContestSeriesId = new Guid("00000000-0000-0000-0004-000000000004"),
                ContestType = ContestType.Literary,
                Number = 20,
                Year = 2022,
                TopicUrl = "/forum/topic/contest-results-lit-20",
                IsActive = true
            },
            new ContestSeries
            {
                ContestSeriesId = new Guid("00000000-0000-0000-0004-000000000005"),
                ContestType = ContestType.Art,
                Number = 2,
                Year = 2024,
                TopicUrl = "/forum/topic/contest-results-art-2",
                IsActive = true
            },
            new ContestSeries
            {
                ContestSeriesId = new Guid("00000000-0000-0000-0004-000000000006"),
                ContestType = ContestType.Art,
                Number = 1,
                Year = 2023,
                TopicUrl = "/forum/topic/contest-results-art-1",
                IsActive = true
            });

        // The system author every automated action is attributed to. Cannot log in:
        // no salt, no hash.
        modelBuilder.Entity<User>().HasData(
            new User
            {
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Username = SystemUser.Username,
                Email = SystemUser.Email,
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
        // Every lookup of a mailed confirmation goes by this hash, and two live
        // tokens may not share one. Filtered, because an invitation carries no
        // secret at all: it is redeemed by the addressee while signed in, and its
        // identifier is not a credential.
        modelBuilder.Entity<Token>()
            .HasIndex(t => t.SecretHash)
            .HasDatabaseName("IX_Tokens_SecretHash")
            .IsUnique()
            .HasFilter("\"SecretHash\" IS NOT NULL");
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

        // IsNewbie as a stored computed column: the schema's copy of
        // ProbationPolicy.NewbiePostThreshold, which SQL cannot read. The number is
        // repeated here, in the migration and in both snapshots, and an
        // architecture test compares all four with the constant.
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
        // index over an expression, and hand-written SQL in the migration would
        // be lost silently the next time the migration is regenerated, so
        // ExpressionIndexInitializer asserts them at startup instead. Declaring a
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

            // Activation is looked up by the hash of the mailed secret, which is
            // also the only form of it the row holds.
            entity.HasIndex(p => p.SecretHash)
                .HasDatabaseName("IX_PendingRegistrations_SecretHash")
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

            // Cascade, not SetNull. The CHECK below requires a PostAttachment row
            // to name a post, so clearing the column on a hard delete produces a
            // row the constraint refuses: the delete fails, and the transaction
            // that carried it fails with it. Unreachable today — posts are only
            // ever soft-deleted, and the attachment rows are soft-deleted with
            // them — but a schema that contradicts itself is a trap waiting for
            // the first caller who does reach it.
            entity.HasOne<DM.Infrastructure.Persistence.Entities.Game.Posts.Post>()
                .WithMany()
                .HasForeignKey(u => u.TargetPostId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            entity.HasIndex(u => u.TargetUserId)
                .HasFilter("\"TargetUserId\" IS NOT NULL");
            entity.HasIndex(u => u.TargetCharacterId)
                .HasFilter("\"TargetCharacterId\" IS NOT NULL");
            entity.HasIndex(u => u.TargetPostId)
                .HasFilter("\"TargetPostId\" IS NOT NULL");

            // A character has at most one live portrait. It owns no column pointing
            // at one — the portrait is whichever CharacterAvatar row still points at
            // the character — so a second live row is not an extra picture but a
            // second answer to one question, and the batch read of a room used to
            // fail outright on the pair. Partial on Type = 2 (CharacterAvatar) and on
            // the live rows, because the portrait a character replaces stays in the
            // table until the orphan sweeper drops it. Named, because the non-unique
            // index above already holds the default name for this column.
            entity.HasIndex(u => u.TargetCharacterId, "IX_Uploads_TargetCharacterId_Live")
                .IsUnique()
                .HasFilter("\"Type\" = 2 AND \"IsRemoved\" = false");

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

        // A character holds one value per attribute specification. The update
        // path reads the stored rows into a dictionary keyed by AttributeId and
        // writes them as check-then-insert, so a second row for the same pair
        // both loses one of the two values and makes every later edit of that
        // character fail on the duplicate key. The composite replaces the
        // conventional CharacterId index: it is the leading column, so the
        // per-character lookups keep their index.
        modelBuilder.Entity<CharacterAttribute>()
            .HasIndex(a => new { a.CharacterId, a.AttributeId }).IsUnique();

        #region Migrated document collections (W1.1)

        // The nine collections that used to live in the document store, as
        // tables. Everything below follows the W1.1 design
        // (DATA_STORAGE.md): jsonb only where the shape is an
        // honest document, atomicity by constraint rather than by client retry,
        // retention by the sweep service instead of TTL indexes.

        // UserSessions: a row per session, out of the document-per-user with an
        // array. The FK index the convention adds serves the device list and
        // RemoveAllSessions; ExpirationUtc serves the hourly purge.
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(s => s.SessionId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(s => s.ExpirationUtc);
        });

        // UserSettings: a row per user, keyed by the user. Absence of the row is
        // UserSettings.Default. The paging numbers are NOT NULL columns so a
        // partial row is unrepresentable; the channel preferences are nullable
        // objects with a category set inside — a document shape, hence jsonb.
        modelBuilder.Entity<UserSettings>(entity =>
        {
            entity.HasKey(s => s.UserId);
            entity.HasOne<User>()
                .WithOne()
                .HasForeignKey<UserSettings>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(s => s.DiscordPreferences).IsJson(isPostgres);
            entity.Property(s => s.TelegramPreferences).IsJson(isPostgres);
            entity.Property(s => s.EmailPreferences).IsJson(isPostgres);
        });

        // UnreadCounters: the primary key is the triple every upsert addresses,
        // so encountering upserts are settled by the server (ON CONFLICT), with
        // no client retry. No FK on EntityId/ParentId: the reference is
        // polymorphic by EntryType, and integrity stays on the application like
        // every polymorphic reference of the project. The partial RemovedUtc
        // index is what the tombstone retention sweep reads.
        modelBuilder.Entity<UnreadCounter>(entity =>
        {
            entity.HasKey(c => new { c.UserId, c.EntityId, c.EntryType });
            entity.HasIndex(c => new { c.UserId, c.ParentId, c.EntryType, c.IsRemoved });
            entity.HasIndex(c => new { c.EntityId, c.EntryType });
            entity.HasIndex(c => new { c.ParentId, c.EntryType });
            var tombstoneIndex = entity.HasIndex(c => c.RemovedUtc);
            if (isPostgres)
            {
                tombstoneIndex.HasFilter("\"RemovedUtc\" IS NOT NULL");
            }
        });

        // Notifications: one row per logical notification plus a row per
        // recipient, out of the document with two user arrays. (CreatedUtc)
        // serves retention and the list sort; (UserId, IsRead) serves the badge.
        modelBuilder.Entity<Entities.Personal.Notifications.Notification>(entity =>
        {
            entity.HasKey(n => n.NotificationId);
            entity.HasIndex(n => n.CreatedUtc);

            // One row per (event, output type): what makes the write of a
            // redelivered bus event idempotent as a constraint rather than a
            // habit of the code. Partial, because NULL marks rows from messages
            // that predate the key and those must never collide.
            var eventIndex = entity.HasIndex(n => new { n.EventId, n.EventType }).IsUnique();
            if (isPostgres)
            {
                eventIndex.HasFilter("\"EventId\" IS NOT NULL");
            }
            var metadata = entity.Property(n => n.Metadata);
            if (isPostgres)
            {
                metadata.HasColumnType("jsonb");
            }
            entity.HasMany(n => n.Recipients)
                .WithOne()
                .HasForeignKey(r => r.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationRecipient>(entity =>
        {
            entity.HasKey(r => new { r.NotificationId, r.UserId });
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(r => new { r.UserId, r.IsRead });
        });

        // OutboxEvents: the transactional outbox of the domain event bus. The
        // one partial index serves both the relay's claim (filter + order +
        // LIMIT) and the backlog metrics (COUNT, MIN(OccurredUtc)); there is
        // deliberately no second index for the retention delete of published
        // rows - the table holds about a week of events, and an hourly seq
        // scan is cheaper than a second index on every insert.
        modelBuilder.Entity<Entities.Outbox.OutboxEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Attempts).HasDefaultValue(0);
            var pendingIndex = entity
                .HasIndex(e => new { e.OccurredUtc, e.Id })
                .HasDatabaseName("IX_OutboxEvents_Pending");
            if (isPostgres)
            {
                pendingIndex.HasFilter("\"PublishedUtc\" IS NULL");
            }
        });

        // Polls: normalized into three tables. The PollVotes primary key
        // (PollId, UserId) is the rule "one voter, one option" — the write is
        // INSERT ... ON CONFLICT DO NOTHING, and the FKs make a ghost vote
        // (voter, option or poll that does not exist) unrepresentable, which is
        // what buried the seeder's self-healing block.
        modelBuilder.Entity<Poll>(entity =>
        {
            entity.HasKey(p => p.PollId);
            entity.HasIndex(p => new { p.IsRemoved, p.StartsUtc });
            entity.HasIndex(p => new { p.IsRemoved, p.EndsUtc });
            entity.HasMany(p => p.Options)
                .WithOne()
                .HasForeignKey(o => o.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PollOption>(entity =>
        {
            entity.HasKey(o => o.PollOptionId);
            entity.HasMany(o => o.Votes)
                .WithOne()
                .HasForeignKey(v => v.PollOptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PollVote>(entity =>
        {
            entity.HasKey(v => new { v.PollId, v.UserId });
            entity.HasOne<Poll>()
                .WithMany()
                .HasForeignKey(v => v.PollId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // LoginAttempts: keyed by the composite string the domain forms
        // (email|address). Email serves ResetAttempts(email), LastAttemptUtc
        // serves the retention sweep.
        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.HasKey(a => a.Key);
            entity.HasIndex(a => a.Email);
            entity.HasIndex(a => a.LastAttemptUtc);
        });

        // The second factor, in three tables. None of them widens the user row:
        // that row is read on every request with a session, and a secret put
        // there would ride along on all of them.
        //
        // UserTwoFactors is keyed by the account, which is the whole "one factor
        // per person" rule expressed as a constraint. RemovalDueUtc carries a
        // partial index because the only read of it is the sweep asking "whose
        // waiting period is over", and that is a handful of rows out of the table.
        modelBuilder.Entity<UserTwoFactor>(entity =>
        {
            entity.HasKey(f => f.UserId);
            entity.HasOne<User>()
                .WithOne()
                .HasForeignKey<UserTwoFactor>(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            var dueIndex = entity.HasIndex(f => f.RemovalDueUtc);
            if (isPostgres)
            {
                dueIndex.HasFilter("\"RemovalDueUtc\" IS NOT NULL");
            }
        });

        // UserTwoFactorRecoveryCodes: ten rows per account, read as a set and
        // written as a set. The FK index the convention adds is what serves both.
        modelBuilder.Entity<UserTwoFactorRecoveryCode>(entity =>
        {
            entity.HasKey(c => c.RecoveryCodeId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TwoFactorChallenges: a stream with a term. CreatedUtc serves the
        // retention sweep; the FK index serves nothing today and exists because
        // the cascade from a deleted account needs it not to scan.
        modelBuilder.Entity<TwoFactorChallenge>(entity =>
        {
            entity.HasKey(c => c.ChallengeId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(c => c.CreatedUtc);
        });

        // SecurityAuditEntries: append-only stream, read as "this user's
        // events, newest first" — the compound index serves the equality and
        // hands the sort back ordered.
        modelBuilder.Entity<SecurityAuditEntry>(entity =>
        {
            entity.HasKey(e => e.SecurityAuditEntryId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.TimestampUtc })
                .IsDescending(false, true);
        });

        // DiceRolls: written only inside the post's transaction (INV-6). The FK
        // index the convention adds serves the room render's batch read.
        modelBuilder.Entity<DiceRoll>(entity =>
        {
            entity.HasKey(d => d.DiceRollId);
            entity.HasOne<Post>()
                .WithMany()
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(d => d.Result).IsJson(isPostgres);
        });

        // AttributeSchemata: an honest document — polymorphic constraints and
        // nested value lists in one jsonb column. SetNull on the author: the
        // schema can be public and outlive its author's account.
        modelBuilder.Entity<AttributeSchema>(entity =>
        {
            entity.HasKey(s => s.AttributeSchemaId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(s => s.Specifications).IsJson(isPostgres);
        });

        // INV-8: a game's schema reference carries a real FK now. Restrict, not
        // cascade or set-null: the schema is soft-deleted, so the row outlives
        // the delete and the game keeps resolving it.
        modelBuilder.Entity<Entities.Game.Game>()
            .HasOne<AttributeSchema>()
            .WithMany()
            .HasForeignKey(g => g.AttributeSchemaId)
            .OnDelete(DeleteBehavior.Restrict);

        #endregion

        // Global Query Filter: automatically exclude soft-deleted entities.
        // UnreadCounters and AttributeSchemata opt out on purpose: their write
        // paths have to see tombstone rows (an upsert over a tombstone revives
        // the marker for a re-added participant, a game resolves its removed
        // schema through its own reference), so each of their reads owns its
        // IsRemoved predicate explicitly.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType == typeof(UnreadCounter) ||
                entityType.ClrType == typeof(AttributeSchema))
            {
                continue;
            }

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
    /// Unread counters (one row per marker)
    /// </summary>
    public DbSet<UnreadCounter> UnreadCounters { get; set; }

    /// <summary>
    /// Notifications (one row per logical notification)
    /// </summary>
    public DbSet<Notification> Notifications { get; set; }

    /// <summary>
    /// Notification recipients
    /// </summary>
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
    /// post reviews (reviews of posts with ratings)
    /// </summary>
    public DbSet<PostReview> PostReviews { get; set; }

    /// <summary>
    /// Dice rolls (written only inside the post's transaction)
    /// </summary>
    public DbSet<DiceRoll> DiceRolls { get; set; }

    /// <summary>
    /// Character attribute schemata
    /// </summary>
    public DbSet<AttributeSchema> AttributeSchemata { get; set; }

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
