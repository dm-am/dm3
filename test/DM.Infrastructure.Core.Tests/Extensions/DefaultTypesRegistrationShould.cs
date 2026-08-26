using System;
using System.Linq;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Paging;
using DM.Infrastructure.Core.Tests.Extensions.DefaultTypesFixtures;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Extensions;

/// <summary>
/// The MS.DI assembly scan keeps the semantics the Autofac one had.
/// </summary>
/// <remarks>
/// Three of them are load-bearing and each covered a production bug on the
/// Autofac side: the scan must not displace explicit wiring (the pooled
/// DbContext and the typed HttpClient factory were both shadowed once), it must
/// keep every implementation of a shared contract (both S3 client providers),
/// and it must not register what a container could never activate (under
/// ValidateOnBuild every such registration is a startup failure).
/// </remarks>
public class DefaultTypesRegistrationShould
{
    private static IServiceCollection Scanned()
    {
        var services = new ServiceCollection();
        services.AddDefaultTypes(typeof(DefaultTypesRegistrationShould).Assembly);
        return services;
    }

    [Fact]
    public void RegisterAScannedClassAsItselfAndItsInterfaces()
    {
        using var provider = Scanned().BuildServiceProvider();

        provider.GetService<PlainService>().Should().NotBeNull();
        provider.GetService<IPlainContract>().Should().BeOfType<PlainService>();
    }

    [Fact]
    public void RegisterScannedTypesTransient()
    {
        using var provider = Scanned().BuildServiceProvider();

        provider.GetRequiredService<PlainService>()
            .Should().NotBeSameAs(provider.GetRequiredService<PlainService>());
        provider.GetRequiredService<IPlainContract>()
            .Should().NotBeSameAs(provider.GetRequiredService<IPlainContract>());
    }

    [Fact]
    public void LeaveAnExplicitRegistrationTheDefault()
    {
        // The typed-HttpClient shape: the service type is registered through a
        // factory before any scan runs, and the scan must leave it alone -
        // MS.DI hands a single resolution to the last descriptor, so a scanned
        // pair after this one would silently replace the factory.
        var services = new ServiceCollection();
        var explicitInstance = new PlainService();
        services.AddSingleton<IPlainContract>(_ => explicitInstance);

        services.AddDefaultTypes(typeof(DefaultTypesRegistrationShould).Assembly);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IPlainContract>().Should().BeSameAs(explicitInstance);
        provider.GetServices<IPlainContract>().Should().ContainSingle(
            "the scan fills gaps, it does not add a second implementation under " +
            "a contract somebody wired by hand");
    }

    [Fact]
    public void RegisterEveryImplementationOfASharedContract()
    {
        // The S3 case: two providers of one interface, both found by the scan,
        // and the consumer enumerates them to pick one. A skip by service type
        // alone - Scrutor's strategy - would have kept only the first.
        using var provider = Scanned().BuildServiceProvider();

        provider.GetServices<ISharedContract>()
            .Select(implementation => implementation.GetType())
            .Should().BeEquivalentTo([typeof(FirstShared), typeof(SecondShared)]);
    }

    [Fact]
    public void BeIdempotentAcrossRepeatedScans()
    {
        var services = Scanned();
        var registered = services.Count;

        services.AddDefaultTypes(typeof(DefaultTypesRegistrationShould).Assembly);

        services.Count.Should().Be(registered,
            "the same scan twice is the RegisterModuleOnce contract: nothing new");
    }

    [Fact]
    public void LetASecondAssemblyExtendAScanIntroducedContract()
    {
        // Two assemblies implement one contract and each is scanned once. The
        // first scan introduces the service type; the second must still be able
        // to add to it - only registrations made outside the scans are fenced.
        var services = new ServiceCollection();
        services.AddDefaultTypes(typeof(CursorService).Assembly);
        services.AddDefaultTypes(typeof(DefaultTypesRegistrationShould).Assembly);

        services.Where(descriptor => descriptor.ServiceType == typeof(ICursorService))
            .Should().HaveCount(2,
                "the infrastructure implementation and the fixture one both serve the contract");
    }

    [Fact]
    public void ForwardInterfacesToTheSelfRegistration()
    {
        // A module pins the implementation type to a lifetime of its own; the
        // scanned interface must follow that registration rather than construct
        // a per-dependency copy - the mail sender's scope contract in miniature.
        var services = new ServiceCollection();
        services.AddSingleton<PlainService>();

        services.AddDefaultTypes(typeof(DefaultTypesRegistrationShould).Assembly);
        using var provider = services.BuildServiceProvider();

        var self = provider.GetRequiredService<PlainService>();
        provider.GetRequiredService<IPlainContract>().Should().BeSameAs(self);
    }

    [Theory]
    [InlineData(typeof(FixtureException), "exceptions are thrown, not resolved")]
    [InlineData(typeof(FixtureAttribute), "attributes are instantiated by reflection over the members that wear them")]
    [InlineData(typeof(ArrayConstructed), "MS.DI answers IEnumerable, not arrays, so the registration could never be satisfied")]
    [InlineData(typeof(FixtureHostedService), "hosted services are started by the host through AddHostedService")]
    [InlineData(typeof(FixtureDbContext), "a scanned context would shadow the pooled AddDbContextPool registration")]
    [InlineData(typeof(InternallyConstructed), "a non-public constructor is how a type opts out of the scan")]
    [InlineData(typeof(StringlyConstructed), "no container supplies a string, and under ValidateOnBuild the dead registration is a startup failure")]
    [InlineData(typeof(PositionalRecord), "a positional record over value types is data, not a service")]
    public void ExcludeWhatTheContainerMustNeverActivate(Type excluded, string because)
    {
        Scanned().Should().NotContain(
            descriptor => descriptor.ServiceType == excluded, because);
    }

    [Fact]
    public void RegisterATypeWhoseOnlyExoticParametersCarryDefaults()
    {
        // A defaulted string parameter asks the container for nothing.
        using var provider = Scanned().BuildServiceProvider();

        provider.GetRequiredService<DefaultedConstruction>().Name.Should().Be("default");
    }
}
