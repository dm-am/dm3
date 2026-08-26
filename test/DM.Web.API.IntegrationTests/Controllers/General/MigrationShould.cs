using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// The project keeps exactly one migration and regenerates it whenever the model
/// changes, which is only safe while no production data exists. That convention
/// is a habit; this is the check that makes it an invariant.
/// </summary>
public class MigrationShould : IntegrationTestBase
{
    public MigrationShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public void MatchTheModelItBuildsTheSchemaFrom()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        // Without this, an entity change that nobody queries goes unnoticed: the
        // migration keeps building the old schema, and every test passes until
        // the first request touches the column that was never created. A new
        // nullable column, a changed index and changed seed data all fall in
        // that gap.
        dbContext.Database.HasPendingModelChanges().Should().BeFalse(
            "InitialCreate is regenerated from the model, so a difference means " +
            "an entity was changed without `dotnet ef migrations remove` and `add`");
    }
}
