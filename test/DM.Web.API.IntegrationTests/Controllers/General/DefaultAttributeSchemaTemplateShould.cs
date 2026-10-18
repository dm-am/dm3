using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.InitialData;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A database built by the migration alone offers a character sheet to start a
/// game from.
/// </summary>
/// <remarks>
/// The creation form starts a game from the default template of the catalogue,
/// and only the development seed wrote one: a site built from the migration had
/// an empty catalogue and no sheet to offer. The row ships with the schema now,
/// so this reads a throwaway database no seed has touched, and reads it through
/// the model, which is also what proves the document the migration carries is
/// one the column converter can read back.
/// </remarks>
public class DefaultAttributeSchemaTemplateShould : IntegrationTestBase
{
    public DefaultAttributeSchemaTemplateShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ShipTheDefaultTemplateWithTheSchema()
    {
        var databaseName = $"dm3_migration_{Guid.NewGuid():N}";
        await using var db = DatabaseFixture.CreateDbContextFor(databaseName);
        try
        {
            await db.Database.MigrateAsync();

            var catalogue = await db.AttributeSchemaTemplates.AsNoTracking().ToListAsync();

            var template = catalogue.Should().ContainSingle(
                "the migration ships exactly one template, and it is the one the form starts from").Subject;
            template.TemplateId.Should().Be(DefaultAttributeSchema.TemplateId);
            template.Title.Should().Be(DefaultAttributeSchema.Title);
            template.IsDefault.Should().BeTrue();

            template.Specifications
                .Select(s => (s.Id, s.Title, s.Order, Kind: s.Constraints.GetType()))
                .Should().Equal(DefaultAttributeSchema.Specifications()
                    .Select(s => (s.Id, s.Title, s.Order, Kind: s.Constraints.GetType())),
                    "the row in the migration is the sheet the seed gives every game, field for field");
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
