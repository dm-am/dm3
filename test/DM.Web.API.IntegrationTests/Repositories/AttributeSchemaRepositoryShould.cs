using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// AttributeSchema declares IRemovable, so the flag has to be what the delete
/// writes and what the reads consult — the entity opts out of the global
/// soft-delete filter precisely so a game keeps resolving a removed schema.
/// Games.AttributeSchemaId carries a real foreign key now (INV-8), and both
/// facts only exist against a live Postgres.
/// </summary>
public class AttributeSchemaRepositoryShould : IntegrationTestBase
{
    public AttributeSchemaRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task MarkASchemaRemovedInsteadOfDroppingTheRow()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var authorId = await SeedUser(dbContext);
        var created = await repository.Create(new CreateAttributeSchema
        {
            Title = "Schema under delete",
            Type = SchemaType.Private,
            Specifications = []
        }, authorId);

        await repository.Delete(created.Id);

        var row = await dbContext.AttributeSchemata
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AttributeSchemaId == created.Id);

        row.Should().NotBeNull(
            "a game row can still reference the schema through the FK, so the row outlives the delete");
        row!.IsRemoved.Should().BeTrue(
            "the delete writes the flag the class declares");

        (await repository.GetSchema(created.Id)).Should().BeNull(
            "a removed schema is invisible to the single read, exactly as a dropped one was");
        (await repository.GetSchemata(authorId)).Should().NotContain(s => s.Id == created.Id,
            "a removed schema is invisible to the list read");
    }

    /// <summary>
    /// The delete gate is a question about the games table, and it has to answer
    /// for a game the schema's author has nothing to do with — which is exactly
    /// what the caller-narrowed query cannot do.
    /// </summary>
    [Fact]
    public async Task SeeAGameOfAnyOwnerThatStillReferencesTheSchema()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var authorId = await SeedUser(dbContext);
        var schema = await repository.Create(new CreateAttributeSchema
        {
            Title = "Schema a stranger builds on",
            Type = SchemaType.Public,
            Specifications = []
        }, authorId);

        (await repository.IsUsedByAnyGame(schema.Id)).Should().BeFalse(
            "nothing references a freshly created schema");

        var masterId = await SeedUser(dbContext);
        var gameId = await SeedGame(dbContext, masterId, schema.Id);

        (await repository.IsUsedByAnyGame(schema.Id)).Should().BeTrue(
            "a live game points at the schema, whoever leads it");
        (await repository.IsUsedByUserGame(schema.Id, authorId)).Should().BeFalse(
            "the author leads no game on it, which is why that query cannot gate the delete");

        var game = await dbContext.Set<DbGame>().FindAsync(gameId);
        game!.IsRemoved = true;
        await dbContext.SaveChangesAsync();

        (await repository.IsUsedByAnyGame(schema.Id)).Should().BeFalse(
            "a removed game holds nothing alive");
    }

    /// <summary>
    /// INV-8: the reference is a foreign key. A game pointing at a schema the
    /// database does not hold is refused instead of dangling.
    /// </summary>
    [Fact]
    public async Task RefuseAGameReferencingASchemaThatDoesNotExist()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var masterId = await SeedUser(dbContext);

        var act = () => SeedGame(dbContext, masterId, Guid.NewGuid());

        await act.Should().ThrowAsync<DbUpdateException>(
            "the dangling reference nothing used to check is unrepresentable now");
    }

    /// <summary>
    /// The soft delete is what makes the FK livable: the schema row survives
    /// the delete, so the game built on it keeps resolving it through its own
    /// reference while the lists no longer offer it.
    /// </summary>
    [Fact]
    public async Task KeepAGameReadableAfterItsSchemaIsRemoved()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var authorId = await SeedUser(dbContext);
        var schema = await repository.Create(new CreateAttributeSchema
        {
            Title = "Schema a game outlives",
            Type = SchemaType.Public,
            Specifications = []
        }, authorId);
        var masterId = await SeedUser(dbContext);
        var gameId = await SeedGame(dbContext, masterId, schema.Id);

        await repository.Delete(schema.Id);

        var resolved = await repository.GetGameSchema(gameId);
        resolved.Should().NotBeNull();
        resolved.Id.Should().Be(schema.Id,
            "the game's own reference resolves a schema its author removed from the lists");
    }

    private static async Task<Guid> SeedUser(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"m{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedGame(DmDbContext dbContext, Guid masterId, Guid schemaId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var gameId = Guid.NewGuid();
        dbContext.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = $"s{suffix}",
            MasterId = masterId,
            Title = $"Game on a schema {suffix}",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
            AttributeSchemaId = schemaId,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return gameId;
    }
}
