using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.InitialData;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

/// <summary>
/// The game module of the model: games and what links to them, rooms, posts,
/// characters, the reviews of games and posts, and the character sheets.
/// </summary>
internal static class GameModel
{
    internal static void Configure(ModelBuilder modelBuilder, bool isPostgres)
    {
        Games(modelBuilder);
        Rooms(modelBuilder);
        Posts(modelBuilder);
        Characters(modelBuilder);
        Reviews(modelBuilder);
        AttributeSchemas(modelBuilder, isPostgres);
    }

    private static void Games(ModelBuilder modelBuilder)
    {
        // A person assists a game once, even when two accepts of one invitation race.
        // The blog's index is in BlogModel.
        modelBuilder.Entity<GameAssistant>()
            .HasIndex(a => new { a.GameId, a.UserId })
            .IsUnique();

        // Pages resolve by the readable key. The blog's index is in BlogModel.
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.PublicId)
            .IsUnique();

        // A game carries a tag once: the required-tag filter counts rows, not distinct
        // tags. Leading with GameId, so it replaces the conventional FK index.
        modelBuilder.Entity<GameTag>()
            .HasIndex(t => new { t.GameId, t.TagId })
            .IsUnique();

        // Adding is check-then-insert, so only the constraint stops two concurrent adds.
        // Leading with GameId, so it replaces the conventional FK index.
        modelBuilder.Entity<GameBlacklist>()
            .HasIndex(b => new { b.GameId, b.BlockedUserId }).IsUnique();
    }

    private static void Rooms(ModelBuilder modelBuilder)
    {
        // RoomNumber is the address key, allocated as MAX+1 within the game, so the
        // constraint stops two concurrent creates sharing a number. It also replaces
        // the plain GameId index.
        modelBuilder.Entity<Room>()
            .HasIndex(r => new { r.GameId, r.RoomNumber })
            .IsUnique();

        // Off by default: a closed room is named to everybody unless its master
        // says otherwise, and a row written without the column has to mean that.
        modelBuilder.Entity<Room>()
            .Property(r => r.HiddenWithoutAccess)
            .HasDefaultValue(false);

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
    }

    private static void Posts(ModelBuilder modelBuilder)
    {
        AuditRelationships.DeletedBy<Post>(modelBuilder);

        // A page of posts is read by parent, ordered by CreatedUtc and the identifier,
        // so the cost grows with the page, not the room; the comments' half is in
        // SharedModel. Unfiltered, because it replaces the plain foreign key index.
        modelBuilder.Entity<Post>()
            .HasIndex(p => new { p.RoomId, p.CreatedUtc, p.PostId });
    }

    private static void Characters(ModelBuilder modelBuilder)
    {
        AuditRelationships.DeletedBy<Character>(modelBuilder);

        // One value per attribute: the update path writes check-then-insert. CharacterId
        // leads, so the per-character lookups keep their index.
        modelBuilder.Entity<CharacterAttribute>()
            .HasIndex(a => new { a.CharacterId, a.AttributeId }).IsUnique();
    }

    private static void Reviews(ModelBuilder modelBuilder)
    {
        // One active review per author-game pair
        modelBuilder.Entity<GameReview>()
            .HasIndex(r => new { r.AuthorId, r.GameId })
            .HasLiveRowsFilter()
            .IsUnique();

        // Index for efficient lookup by game (reviews of a game)
        modelBuilder.Entity<GameReview>()
            .HasIndex(r => r.GameId)
            .HasLiveRowsFilter();

        // One active review per author-post pair
        modelBuilder.Entity<PostReview>()
            .HasIndex(r => new { r.AuthorId, r.PostId })
            .HasLiveRowsFilter()
            .IsUnique();

        // Index for reviews by PostAuthorId (for "reviews ON user's posts" queries)
        modelBuilder.Entity<PostReview>()
            .HasIndex(r => r.PostAuthorId)
            .HasLiveRowsFilter();

        // Index for reviews by GameId (for "reviews in game" queries)
        modelBuilder.Entity<PostReview>()
            .HasIndex(r => r.GameId)
            .HasLiveRowsFilter();

        // Index for reviews by PostId (for "reviews of a post" queries)
        modelBuilder.Entity<PostReview>()
            .HasIndex(r => r.PostId)
            .HasLiveRowsFilter();

        AuditRelationships.Editable<GameReview>(modelBuilder);
        AuditRelationships.Editable<PostReview>(modelBuilder);
    }

    private static void AttributeSchemas(ModelBuilder modelBuilder, bool isPostgres)
    {
        // SetNull on the author: the template belongs to the site.
        modelBuilder.Entity<AttributeSchemaTemplate>(entity =>
        {
            entity.HasKey(t => t.TemplateId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(t => t.Specifications).IsJson(isPostgres);

            // Two templates under one name are indistinguishable in the list
            // the creation form offers.
            entity.HasIndex(t => t.Title)
                .IsUnique()
                .HasLiveRowsFilter();

            // At most one default. The other half of the rule — at least one
            // — is held by the row below and by a removal that refuses the
            // default template.
            entity.HasIndex(t => t.IsDefault)
                .IsUnique()
                .HasFilter("\"IsDefault\" = true and \"IsRemoved\" = false");

            // The catalogue is never empty: the creation form starts a game from
            // the default template, so a site without one has no sheet to offer.
            entity.HasData(DefaultAttributeSchema.Template());
        });

        // GameAttributeSchemas: the character sheet of one game, keyed by the
        // game so a second sheet for it is unrepresentable.
        modelBuilder.Entity<Entities.Game.GameAttributeSchema>(entity =>
        {
            entity.HasKey(s => s.GameId);
            entity.HasOne<Entities.Game.Game>()
                .WithOne()
                .HasForeignKey<Entities.Game.GameAttributeSchema>(s => s.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(s => s.Specifications).IsJson(isPostgres);
        });
    }
}
