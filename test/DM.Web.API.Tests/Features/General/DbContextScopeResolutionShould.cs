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

/// <summary>
/// Guards the DbContext scope contract against the scan-shadowing regression:
/// AddDbContextPool registers DmDbContext as a scoped service, while the
/// persistence module's blanket AddDefaultTypes scan sweeps the same assembly.
/// Under Autofac the scan once silently replaced the pooled scoped registration
/// with per-dependency construction: every consumer (each repository, each
/// GetRequiredService call) received its own context, transactions never
/// spanned service and repository, and identity resolution hid other consumers'
/// writes. The MS.DI scan refuses DbContext descendants outright; this holds it
/// to that.
/// </summary>
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
    public void ResolveSingleDbContextInstancePerScope()
    {
        using var scope = _provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var second = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        second.Should().BeSameAs(first,
            "one DI scope must hold exactly one DmDbContext instance");
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

    [Fact]
    public void ResolveDistinctDbContextsInDifferentScopes()
    {
        using var firstScope = _provider.CreateScope();
        using var secondScope = _provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<DmDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<DmDbContext>();

        second.Should().NotBeSameAs(first,
            "the context is scoped, not a singleton");
    }

    [Fact]
    public void LeaseDbContextsFromThePool()
    {
        using var scope = _provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        context.ContextId.Lease.Should().BePositive(
            "AddDbContextPool must actually engage the pool — a zero lease means " +
            "the instance was constructed outside of it");
    }

    /// <summary>
    /// The scan itself keeps its hands off the context: no descriptor of the
    /// module's making names DmDbContext, so nothing can outrank the pool.
    /// </summary>
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

    /// <summary>
    /// The context field is private implementation detail of every repository;
    /// identity is the only thing the contract cares about, so reflection here
    /// is confined to "find the single DmDbContext the instance holds".
    /// </summary>
    private static DmDbContext ExtractDbContext(object repository) =>
        repository.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.GetValue(repository))
            .OfType<DmDbContext>()
            .Single();

    public void Dispose() => _provider.Dispose();
}
