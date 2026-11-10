using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Personal.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

internal static class PersonalModel
{
    internal static void Configure(ModelBuilder modelBuilder, bool isPostgres)
    {
        modelBuilder.Entity<UserBlacklist>()
            .HasIndex(b => new { b.OwnerId, b.BlockedUserId }).IsUnique();

        modelBuilder.Entity<UserProfileNote>()
            .HasIndex(n => new { n.OwnerId, n.SubjectUserId }).IsUnique();

        // Notepad scope lookup: entries are always fetched for a concrete
        // notepad (container + type + owner for player notepads).
        modelBuilder.Entity<NotepadEntry>()
            .HasIndex(e => new { e.ContainerId, e.NotepadType, e.OwnerId });

        Notifications(modelBuilder, isPostgres);
    }

    private static void Notifications(ModelBuilder modelBuilder, bool isPostgres)
    {
        // Notifications: one row per logical notification plus a row per
        // recipient, out of the document with two user arrays. (CreatedUtc)
        // serves retention and the list sort; (UserId, IsRead) serves the badge.
        modelBuilder.Entity<Entities.Personal.Notifications.Notification>(entity =>
        {
            entity.HasIndex(n => n.CreatedUtc);

            // One row per (event, output type): what makes the write of a
            // redelivered bus event idempotent as a constraint rather than a
            // habit of the code. Partial, because NULL marks rows written outside
            // the bus, as the seed writes them, and those must never collide.
            entity.HasIndex(n => new { n.EventId, n.EventType }).IsUnique().HasFilter("\"EventId\" IS NOT NULL");
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
    }
}
