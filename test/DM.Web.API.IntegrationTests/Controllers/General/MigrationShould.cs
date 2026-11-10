using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

// The project keeps exactly one migration and regenerates it whenever the model changes, which is
// only safe while no production data exists.
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

        // An entity change nobody queries would leave the migration building the old schema.
        dbContext.Database.HasPendingModelChanges().Should().BeFalse(
            "InitialCreate is regenerated from the model, so a difference means " +
            "an entity was changed without `dotnet ef migrations remove` and `add`");
    }
}
