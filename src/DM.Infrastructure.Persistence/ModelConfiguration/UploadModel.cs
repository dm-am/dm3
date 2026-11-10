using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Shared;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

internal static class UploadModel
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
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

            // CHECK: who sees a file is chosen on a post attachment alone
            // (PostAttachment=3); everybody sees an avatar.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Uploads_AddresseesOnPostAttachment",
                "\"AddresseesJson\" IS NULL OR \"Type\" = 3"));
        });
    }
}
