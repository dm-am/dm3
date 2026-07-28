using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using AutoMapper;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Testing;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// Guards the mapper registration against the same shadowing class as
/// DbContextScopeResolutionShould. RegisterMapper is called once per assembly —
/// ten times in the API — and it used to register the mapper plumbing on every
/// call, so nine registrations were dead weight and only the last was resolved.
/// The API also called AddAutoMapper into MS.DI, which the Autofac registration
/// then overrode, silently discarding the AllowNullCollections it configured.
/// </summary>
public class MapperRegistrationShould : UnitTestBase
{
    private static readonly Assembly PersistenceAssembly = typeof(DmDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Startup).Assembly;

    [Fact]
    public void BuildOneConfigurationNoMatterHowManyAssembliesRegister()
    {
        var builder = new ContainerBuilder();
        builder.RegisterMapper(PersistenceAssembly);
        builder.RegisterMapper(ApiAssembly);

        using var container = builder.Build();

        container.Resolve<IEnumerable<IConfigurationProvider>>().Should().HaveCount(1,
            "the mapper is one object built from every profile, not one per assembly");
    }

    [Fact]
    public void CollectProfilesFromEveryRegisteredAssembly()
    {
        var builder = new ContainerBuilder();
        builder.RegisterMapper(PersistenceAssembly);
        builder.RegisterMapper(ApiAssembly);

        using var container = builder.Build();
        var profileAssemblies = container.Resolve<IEnumerable<Profile>>()
            .Select(profile => profile.GetType().Assembly)
            .Distinct()
            .ToArray();

        profileAssemblies.Should().Contain(PersistenceAssembly).And.Contain(ApiAssembly,
            "registering the plumbing once must not drop the profiles of later assemblies");
    }

    /// <summary>
    /// A null source collection must stay null. Domain code reads that as "the
    /// caller did not send this field", so turning it into an empty collection
    /// makes a PATCH that omits a collection indistinguishable from one that
    /// clears it — which is how omitting `contacts` came to wipe them.
    /// </summary>
    [Fact]
    public void KeepANullSourceCollectionNull()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new ProbeProfile()).As<Profile>();
        builder.RegisterMapper(PersistenceAssembly);

        using var container = builder.Build();
        using var scope = container.BeginLifetimeScope();

        var mapped = scope.Resolve<IMapper>().Map<ProbeTarget>(new ProbeSource());

        mapped.Items.Should().BeNull();
    }

    private class ProbeSource
    {
        public List<string> Items { get; set; } = null!;
    }

    private class ProbeTarget
    {
        public List<string> Items { get; set; } = null!;
    }

    private class ProbeProfile : Profile
    {
        public ProbeProfile() => CreateMap<ProbeSource, ProbeTarget>();
    }
}
