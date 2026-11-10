using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

// Token.EntityId holds a GameId, a BlogId or nothing, so both foreign keys EF would emit on it are
// removed from the migration by hand: PostgreSQL checks every foreign key on a non-null value.
public class InvitationTokenPersistenceShould : IntegrationTestBase
{
    public InvitationTokenPersistenceShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task NotConstrainPolymorphicEntityIdToASingleTable()
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
                    WHERE conrelid = '"Tokens"'::regclass AND contype = 'f'
                    """)
                .ToListAsync();

            foreignKeys.Should().NotContain("FK_Tokens_Blogs_EntityId");
            foreignKeys.Should().NotContain("FK_Tokens_Games_EntityId");

            // The column still carries the FKs that are genuinely single-target.
            foreignKeys.Should().Contain("FK_Tokens_Users_UserId");
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task StoreInvitationTokensForBothGamesAndBlogs()
    {
        var databaseName = $"dm3_migration_{Guid.NewGuid():N}";
        await using var db = DatabaseFixture.CreateDbContextFor(databaseName);
        try
        {
            await db.Database.MigrateAsync();

            var userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                UserId = userId,
                Username = "invitee",
                Email = "invitee@example.com",
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

            // EntityId values that exist in neither Games nor Blogs: with either
            // constraint present this is exactly what fails.
            foreach (var type in new[]
                     {
                         TokenType.GamePlayerInvitation,
                         TokenType.GameReaderInvitation,
                         TokenType.GameAssistantInvitation,
                         TokenType.BlogAssistantInvitation,
                         TokenType.BlogReaderInvitation,
                     })
            {
                db.Set<Token>().Add(new Token
                {
                    TokenId = Guid.NewGuid(),
                    UserId = userId,
                    EntityId = Guid.NewGuid(),
                    CreatedUtc = DateTimeOffset.UtcNow,
                    Type = type,
                    IsRemoved = false,
                });
            }

            var save = async () => await db.SaveChangesAsync();
            await save.Should().NotThrowAsync();
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
