using System;
using System.Linq;
using System.Reflection;
using DM.Domain.Core.Abstractions;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

// AddDbContextPool registers DmDbContext scoped, and the module's AddDefaultTypes scan of the same
// assembly must not shadow it: one context per scope.
public class DbContextScopeResolutionShould : UnitTestBase, IDisposable
{
    private readonly ServiceProvider _provider;

    public DbContextScopeResolutionShould()
    {
        // The real production wiring shape: the pool registration first, the
        // module after - exactly the order Startup composes them in.
        var services = new ServiceCollection();
        services.AddDbContextPool<DmDbContext>(options =>
            options.UseNpgsql("Host=localhost;Database=di-probe;Username=probe;Password=probe"));
        services.AddDmPersistence();
        services.AddSingleton<IGuidFactory>(Mock<IGuidFactory>());
        services.AddSingleton<IDateTimeProvider>(Mock<IDateTimeProvider>());

        _provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void InjectTheScopeDbContextIntoRepositories()
    {
        using var scope = _provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();

        ExtractDbContext(repository).Should().BeSameAs(context,
            "a repository must work on the same context the rest of its scope sees, " +
            "otherwise cross-repository transactions and identity resolution silently break");
    }

    // The scan itself keeps its hands off the context: no descriptor of the module's making names
    // DmDbContext, so nothing can outrank the pool.
    [Fact]
    public void LeaveTheContextRegistrationToThePoolAlone()
    {
        var services = new ServiceCollection();
        services.AddDmPersistence();

        services.Where(descriptor => descriptor.ServiceType == typeof(DmDbContext))
            .Should().BeEmpty(
                "the blanket scan must refuse DbContext descendants - a scanned " +
                "registration after the pool's would silently replace it");
    }

    private static DmDbContext ExtractDbContext(object repository) =>
        repository.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.GetValue(repository))
            .OfType<DmDbContext>()
            .Single();

    public void Dispose() => _provider.Dispose();
}
