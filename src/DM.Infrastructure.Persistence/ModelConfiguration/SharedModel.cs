using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

/// <summary>
/// What every module shares in the model: comments, subscriptions, likes, the
/// unread markers and the outbox of domain events.
/// </summary>
internal static class SharedModel
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        Comments(modelBuilder);
        Subscriptions(modelBuilder);
        Likes(modelBuilder);
        UnreadCounters(modelBuilder);
        Outbox(modelBuilder);
    }

    private static void Comments(ModelBuilder modelBuilder)
    {
        AuditRelationships.DeletedBy<Comment>(modelBuilder);

        // The comments' half of the page index whose reason is beside the posts' half
        // in GameModel.
        modelBuilder.Entity<Comment>()
            .HasIndex(c => new { c.EntityId, c.CreatedUtc, c.CommentId });
    }

    private static void Subscriptions(ModelBuilder modelBuilder)
    {
        // Serves "who follows this target", asked on every profile, game and blog page.
        modelBuilder.Entity<Subscription>()
            .HasIndex(s => new { s.TargetType, s.TargetId });

        // One subscription per (subscriber, target): subscribing is check-then-insert.
        // Leads with SubscriberId, so it replaces the plain FK index.
        modelBuilder.Entity<Subscription>()
            .HasIndex(s => new { s.SubscriberId, s.TargetType, s.TargetId })
            .IsUnique();
    }

    private static void Likes(ModelBuilder modelBuilder)
    {
        // One live like per (entity, reader): liking is check-then-insert. Partial on
        // live rows, because unliking sets IsRemoved and liking again is allowed.
        // EntityType leads: the aggregates read likes of one kind joined on EntityId.
        modelBuilder.Entity<Like>()
            .HasIndex(l => new { l.EntityType, l.EntityId, l.UserId })
            .HasLiveRowsFilter()
            .IsUnique();
    }

    private static void UnreadCounters(ModelBuilder modelBuilder)
    {
        // The key is the triple every upsert addresses (ON CONFLICT). No FK on EntityId and
        // ParentId: they are polymorphic by EntryType. The RemovedUtc index serves the tombstone sweep.
        modelBuilder.Entity<UnreadCounter>(entity =>
        {
            entity.HasKey(c => new { c.UserId, c.EntityId, c.EntryType });
            entity.HasIndex(c => new { c.UserId, c.ParentId, c.EntryType, c.IsRemoved });
            entity.HasIndex(c => new { c.EntityId, c.EntryType });
            entity.HasIndex(c => new { c.ParentId, c.EntryType });
            entity.HasIndex(c => c.RemovedUtc).HasFilter("\"RemovedUtc\" IS NOT NULL");
        });
    }

    private static void Outbox(ModelBuilder modelBuilder)
    {
        // One partial index serves the relay claim and the backlog metrics; the retention
        // delete of a week of rows scans rather than pay a second index on every insert.
        modelBuilder.Entity<Entities.Outbox.OutboxEvent>(entity =>
        {
            entity.Property(e => e.Attempts).HasDefaultValue(0);
            entity
                .HasIndex(e => new { e.OccurredUtc, e.Id })
                .HasDatabaseName("IX_OutboxEvents_Pending")
                .HasFilter("\"PublishedUtc\" IS NULL");
        });
    }
}
