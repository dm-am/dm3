using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.MongoIntegration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// AttributeSchema declares IRemovable, so the flag has to be what the delete
/// writes and what the reads consult. Runs against the container Mongo because
/// the document is the only place that answer lives: nothing relational mirrors
/// it, and a mocked collection would assert the mock instead of the driver.
/// </summary>
public class AttributeSchemaRepositoryShould : IntegrationTestBase
{
    public AttributeSchemaRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task MarkASchemaRemovedInsteadOfDroppingTheDocument()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaRepository>();
        var mongoClient = scope.ServiceProvider.GetRequiredService<DmMongoClient>();

        var authorId = Guid.NewGuid();
        var created = await repository.Create(new CreateAttributeSchema
        {
            Title = "Schema under delete",
            Type = SchemaType.Private,
            Specifications = []
        }, authorId);

        await repository.Delete(created.Id);

        var document = await mongoClient.GetCollection<DbAttributeSchema>()
            .Find(Builders<DbAttributeSchema>.Filter.Eq(s => s.Id, created.Id))
            .FirstOrDefaultAsync();

        document.Should().NotBeNull(
            "a game row in Postgres can still reference the schema, so the document outlives the delete");
        document!.IsRemoved.Should().BeTrue(
            "the delete writes the flag the class declares");

        (await repository.GetSchema(created.Id)).Should().BeNull(
            "a removed schema is invisible to the single read, exactly as a dropped one was");
        (await repository.GetSchemata(authorId)).Should().NotContain(s => s.Id == created.Id,
            "a removed schema is invisible to the list read");
    }

    /// <summary>
    /// The delete gate is a relational question about a Mongo document, and no
    /// foreign key spans the two stores: this query is the whole enforcement.
    /// It has to answer for a game the schema's author has nothing to do with,
    /// which is exactly what the caller-narrowed one cannot do.
    /// </summary>
    [Fact]
    public async Task SeeAGameOfAnyOwnerThatStillReferencesTheSchema()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var authorId = Guid.NewGuid();
        var schema = await repository.Create(new CreateAttributeSchema
        {
            Title = "Schema a stranger builds on",
            Type = SchemaType.Public,
            Specifications = []
        }, authorId);

        (await repository.IsUsedByAnyGame(schema.Id)).Should().BeFalse(
            "nothing references a freshly created schema");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var masterId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = masterId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"m{masterId:N}"[..20],
            Email = $"{masterId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow,
        });

        var gameId = Guid.NewGuid();
        dbContext.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = $"s{suffix}",
            MasterId = masterId,
            Title = $"Game on a foreign schema {suffix}",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
            AttributeSchemaId = schema.Id,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

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
}
