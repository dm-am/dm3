using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Token.EntityId is polymorphic: depending on Type it holds a GameId, a BlogId
/// or nothing. Both navigations are mapped on that single column, so EF wants to
/// emit FK_Tokens_Games_EntityId AND FK_Tokens_Blogs_EntityId — and PostgreSQL
/// requires a non-null value to satisfy every foreign key on a column, so an
/// EntityId would have to exist in Games and in Blogs simultaneously. No
/// invitation of any kind could be inserted. Both constraints are therefore
/// removed by hand from the migration.
///
/// This runs against a database built by that migration rather than by the
/// shared fixture: the fixture uses EnsureCreated, which derives the schema from
/// the model and so cannot see hand-edits to the migration at all. That gap is
/// the reason this defect survived — the schema the tests ran on was never the
/// schema production gets.
/// </summary>
public class InvitationTokenPersistenceShould : IntegrationTestBase
{
    public InvitationTokenPersistenceShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private DmDbContext CreateMigratedContext(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(DatabaseFixture.ConnectionString)
        {
            Database = databaseName,
        };
        return new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .Options);
    }

    [Fact]
    public async Task NotConstrainPolymorphicEntityIdToASingleTable()
    {
        var databaseName = $"dm3_migration_{Guid.NewGuid():N}";
        await using var db = CreateMigratedContext(databaseName);
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
        await using var db = CreateMigratedContext(databaseName);
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
