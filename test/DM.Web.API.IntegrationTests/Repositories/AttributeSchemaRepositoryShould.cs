using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.MongoIntegration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;

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
}
