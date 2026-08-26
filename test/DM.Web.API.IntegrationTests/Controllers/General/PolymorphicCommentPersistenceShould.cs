using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Shared;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Comment.EntityId is polymorphic: it holds a TopicId, a GameId, a BlogId or a
/// PublicationId, depending on which endpoint wrote the row. Only Topic.Comments is a
/// mapped navigation (Blog.Comments, Game.Comments and Publication.Comments are
/// [NotMapped] for exactly this reason), so EF emits FK_Comments_Topics_EntityId, and
/// PostgreSQL then rejects every comment that is not on a forum topic: three of the four
/// kinds. The constraint is deleted from the migration by hand, and regenerating the
/// migration writes it back.
///
/// Same shape and same reason as InvitationTokenPersistenceShould: the model keeps the
/// relationship, so the edit lives in the migration body alone, and only a database built
/// by MigrateAsync can be asked whether it survived. MigrationShould cannot see this at
/// all, because HasPendingModelChanges compares the model with the snapshot and both of
/// them keep declaring the relationship.
/// </summary>
public class PolymorphicCommentPersistenceShould : IntegrationTestBase
{
    public PolymorphicCommentPersistenceShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task NotConstrainPolymorphicEntityIdToTopics()
    {
        var databaseName = $"dm3_migration_{Guid.NewGuid():N}";
        await using var db = DatabaseFixture.CreateDbContextFor(databaseName);
        try
        {
            await db.Database.MigrateAsync();

            var foreignKeys = await db.Database
                .SqlQuery<string>($"""
                    SELECT conname AS "Value"
                    FROM pg_constraint
                    WHERE conrelid = '"Comments"'::regclass AND contype = 'f'
                    """)
                .ToListAsync();

            foreignKeys.Should().NotContain("FK_Comments_Topics_EntityId");

            // The table still carries the keys that are genuinely single-target. Without
            // this the assertion above would also hold on an empty result, which is what a
            // renamed table or a broken query would produce.
            foreignKeys.Should().Contain("FK_Comments_Users_AuthorId");
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task StoreCommentsOnEntitiesThatAreNotTopics()
    {
        var databaseName = $"dm3_migration_{Guid.NewGuid():N}";
        await using var db = DatabaseFixture.CreateDbContextFor(databaseName);
        try
        {
            await db.Database.MigrateAsync();

            var authorId = Guid.NewGuid();
            db.Users.Add(new User
            {
                UserId = authorId,
                Username = "commenter",
                Email = "commenter@example.com",
                PasswordHash = "fakehash",
                Salt = "fakesalt",
                PasswordHashVersion = 2,
                Role = UserRole.RegularUser,
                CreatedUtc = DateTimeOffset.UtcNow,
                LastActivityUtc = DateTimeOffset.UtcNow,
                Status = string.Empty,
                Name = string.Empty,
                Location = string.Empty,
                Info = string.Empty,
            });
            await db.SaveChangesAsync();

            // Written the way GameCommentRepository.Create writes a game comment, which is
            // also the shape of a blog and a publication one: EntityId is the id of the
            // commented entity, and for everything except a forum topic that id is in no
            // Topics row. With the constraint present this is exactly what fails.
            db.Comments.Add(new Comment
            {
                CommentId = Guid.NewGuid(),
                EntityId = Guid.NewGuid(),
                AuthorId = authorId,
                CreatedUtc = DateTimeOffset.UtcNow,
                Text = "A comment on something that is not a forum topic.",
                IsRemoved = false,
            });

            var save = async () => await db.SaveChangesAsync();
            await save.Should().NotThrowAsync();
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
