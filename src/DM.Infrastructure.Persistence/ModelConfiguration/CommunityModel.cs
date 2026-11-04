using System;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.InitialData;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

/// <summary>
/// The community module of the model: endorsements, testimonials, the fundraising
/// goal, the indexes of the statistics periods, awards, achievements and polls.
/// </summary>
internal static class CommunityModel
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        Endorsements(modelBuilder);
        Testimonials(modelBuilder);
        Fundraising(modelBuilder);
        StatisticsPeriods(modelBuilder);
        Awards(modelBuilder);
        Achievements(modelBuilder);
        Polls(modelBuilder);
    }

    private static void Endorsements(ModelBuilder modelBuilder)
    {
        // One active endorsement per author-target pair
        modelBuilder.Entity<UserEndorsement>()
            .HasIndex(e => new { e.AuthorId, e.TargetUserId })
            .HasLiveRowsFilter()
            .IsUnique();

        // Index for efficient lookup by target user (endorsements received)
        modelBuilder.Entity<UserEndorsement>()
            .HasIndex(e => e.TargetUserId)
            .HasLiveRowsFilter();

        AuditRelationships.Editable<UserEndorsement>(modelBuilder);
    }

    private static void Testimonials(ModelBuilder modelBuilder)
    {
        // One testimonial per user
        modelBuilder.Entity<WebsiteTestimonial>()
            .HasIndex(t => t.AuthorId)
            .HasLiveRowsFilter()
            .IsUnique();

        AuditRelationships.Editable<WebsiteTestimonial>(modelBuilder);
    }

    private static void Fundraising(ModelBuilder modelBuilder)
    {
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

            // The one row the page reads, empty until the administration announces
            // a collection: a live database starts with nothing collected.
            entity.HasData(new FundraisingGoal
            {
                FundraisingGoalId = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Title = "",
                GoalAmount = 0m,
                CollectedAmount = 0m,
                ModifiedUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
            });
        });
    }

    private static void StatisticsPeriods(ModelBuilder modelBuilder)
    {
        // Partial CreatedUtc indexes backing the statistics period aggregates
        // (leaderboards group live rows by a [start, end) CreatedUtc window on
        // Posts, PostReviews and Publications) — same live-rows-only partial
        // index idiom as the review indexes of GameModel.
        modelBuilder.Entity<Post>()
            .HasIndex(p => p.CreatedUtc)
            .HasLiveRowsFilter();

        modelBuilder.Entity<PostReview>()
            .HasIndex(r => r.CreatedUtc)
            .HasLiveRowsFilter();

        modelBuilder.Entity<Publication>()
            .HasIndex(p => p.CreatedUtc)
            .HasLiveRowsFilter();

        // Period digest markers: one digest per calendar month and one per
        // year. NULLs are distinct in unique indexes,
        // so the two shapes need separate partial unique indexes: monthly
        // digests unique on (Year, Month), the yearly digest unique on Year
        // alone. The generator also checks before inserting; the indexes are
        // the concurrency backstop.
        modelBuilder.Entity<PeriodDigestTopic>(entity =>
        {
            entity.HasIndex(d => new { d.Year, d.Month })
                .IsUnique()
                .HasFilter("\"Month\" IS NOT NULL")
                .HasDatabaseName("IX_PeriodDigestTopics_Year_Month");

            entity.HasIndex(d => d.Year)
                .IsUnique()
                .HasFilter("\"Month\" IS NULL")
                .HasDatabaseName("IX_PeriodDigestTopics_Year");
        });
    }

    private static void Awards(ModelBuilder modelBuilder)
    {
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
            // Restrict, not cascade: the recipient already cascades from User.
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
            entity.HasIndex(a => new { a.UserId, a.AwardedUtc }).HasLiveRowsFilter();
        });

        AuditRelationships.DeletedBy<UserAward>(modelBuilder);

        modelBuilder.Entity<AwardType>().HasData(AwardCatalog.AwardTypes());
        modelBuilder.Entity<ContestSeries>().HasData(AwardCatalog.ContestSeries());
    }

    private static void Achievements(ModelBuilder modelBuilder)
    {
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
            entity.HasIndex(a => new { a.UserId, a.AchievementTypeId }).IsUnique().HasLiveRowsFilter();

            entity.HasIndex(a => new { a.UserId, a.EarnedUtc });
        });

        modelBuilder.Entity<AchievementCategory>().HasData(AchievementCatalog.Categories());
        modelBuilder.Entity<AchievementType>().HasData(AchievementCatalog.Types());
    }

    private static void Polls(ModelBuilder modelBuilder)
    {
        // The PollVotes primary key (PollId, UserId) is the rule "one voter, one
        // option": the write is INSERT ... ON CONFLICT DO NOTHING.
        modelBuilder.Entity<Poll>(entity =>
        {
            entity.HasIndex(p => new { p.IsRemoved, p.StartsUtc });
            entity.HasIndex(p => new { p.IsRemoved, p.EndsUtc });
            entity.HasMany(p => p.Options)
                .WithOne()
                .HasForeignKey(o => o.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PollOption>(entity =>
        {
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
    }
}
