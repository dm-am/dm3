using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.Tests.Shared.Configuration;

// The migration runs once per rollout and holds the schema lock: a statement cut off at
// 30 seconds or replayed by a retry leaves a half-applied step, so it gets neither.
public class MigrationContextShould
{
    [Fact]
    public void HaveNoTimeoutAndNoRetryInMigrationMode()
    {
        using var scope = BuildProvider(migrationMode: true).CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        context.Database.GetCommandTimeout().Should().Be(0, "every command EF creates takes this timeout");
        context.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeFalse();
    }

    [Fact]
    public void KeepTheTimeoutAndTheRetryWhenServing()
    {
        using var scope = BuildProvider(migrationMode: false).CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        context.Database.GetCommandTimeout().Should().Be(30);
        context.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeTrue();
    }

    // Nothing opens a connection: the options are read off the context alone.
    private static ServiceProvider BuildProvider(bool migrationMode)
    {
        var services = new ServiceCollection();
        services.Configure<ConnectionStrings>(strings => strings.Rdb = "Host=localhost;Database=none");
        Startup.AddDatabase(services, migrationMode);
        return services.BuildServiceProvider();
    }
}
