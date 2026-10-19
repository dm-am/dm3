using System.Linq;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.InitialData;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

/// <summary>
/// The moderation module of the model: the catalogue of tags the moderators keep,
/// warnings and the conversations of tickets.
/// </summary>
internal static class ModerationModel
{
    internal static void Configure(ModelBuilder modelBuilder, bool isPostgres)
    {
        Tags(modelBuilder, isPostgres);
        Warnings(modelBuilder);
        Tickets(modelBuilder);
    }

    private static void Tags(ModelBuilder modelBuilder, bool isPostgres)
    {
        // ShortId is the tag's public key: the /games filters compare by it.
        modelBuilder.Entity<Tag>()
            .HasIndex(t => t.ShortId)
            .IsUnique();

        // The sequence TagNumbers draws from, declared on the model so a regenerated
        // migration keeps it. Starts one past the highest number TagCatalog ships.
        if (isPostgres)
        {
            modelBuilder.HasSequence<int>(DmDbContext.TagShortIdSequence)
                .StartsAt(TagCatalog.Tags().Max(t => t.ShortId) + 1);
        }

        modelBuilder.Entity<TagGroup>().HasData(TagCatalog.Groups());
        modelBuilder.Entity<Tag>().HasData(TagCatalog.Tags());
    }

    private static void Warnings(ModelBuilder modelBuilder)
    {
        // No inverse collection and no cascade: the warning outlives the account of
        // whoever corrected its points.
        modelBuilder.Entity<Entities.Moderation.Warning>()
            .HasOne(w => w.PointsChangedBy)
            .WithMany()
            .HasForeignKey(w => w.PointsChangedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // The avatar a warning on a profile pins (DATA_STORAGE.md, "Объектное
        // хранилище"). SetNull, because the sweeper deletes the upload row once
        // moderation deleted the picture and its grace period is over: the
        // warning stays, without the picture, as a record of the decision.
        modelBuilder.Entity<Entities.Moderation.Warning>()
            .HasOne(w => w.ProfileAvatarUpload)
            .WithMany()
            .HasForeignKey(w => w.ProfileAvatarUploadId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void Tickets(ModelBuilder modelBuilder)
    {
        // The conversation is read whole in the order written: the index is that read.
        // Deleting an author is refused, not nulled: a null author marks a guest's comment.
        modelBuilder.Entity<Entities.Moderation.TicketComment>(entity =>
        {
            entity.HasIndex(c => new { c.TicketId, c.CreatedUtc, c.TicketCommentId });

            entity.HasOne(c => c.Ticket)
                .WithMany(t => t.TicketComments)
                .HasForeignKey(c => c.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Author)
                .WithMany()
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
