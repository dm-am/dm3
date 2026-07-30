using System;
using System.IO;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace DM.Architecture.Tests;

/// <summary>
/// Executable copy of the Controller -> ApiService -> Service boundary from
/// PATTERNS.md. Both rules read types out of the IL rather than out of source
/// text, because a domain service reached through a using alias leaves nothing
/// in the file for a textual check to find.
/// </summary>
public class ServiceLayerBoundaryShould
{
    // Every DM assembly the host drags into the output directory. A marker-type
    // list would silently stop covering a Domain project added later. AutoMapper
    // is loaded alongside them because one rule names a type from it, and a rule
    // whose target is absent from the model passes without checking anything.
    private static readonly ArchitectureModel Solution = new ArchLoader()
        .LoadFilteredDirectory(AppContext.BaseDirectory, "DM.*.dll", SearchOption.TopDirectoryOnly)
        .LoadAssembly(typeof(AutoMapper.IMapper).Assembly)
        .Build();

    private static readonly IObjectProvider<Class> Controllers = Classes()
        .That().AreAssignableTo(typeof(ControllerBase))
        .As("MVC controllers");

    // Matched on the declaring assembly rather than the namespace: five compliant
    // controllers import DM.Domain.*.Features.* for query DTOs while injecting
    // nothing but an ApiService, and a namespace rule would flag them.
    //
    // Every abstraction the domain publishes, not only the ones whose name ends
    // in Service: a controller reaching straight for I*Repository, IIntentionManager
    // or IGameRoleResolver skips the same layer for the same reason, and a rule
    // that lets those through invites the next bypass to be named differently.
    private static readonly IObjectProvider<Interface> DomainAbstractions = Interfaces()
        .That().FollowCustomPredicate(
            i => i.Assembly.Name.StartsWith("DM.Domain.", StringComparison.Ordinal)
                 && i.Name.StartsWith("I", StringComparison.Ordinal),
            "are interfaces declared in a DM.Domain.* assembly")
        .As("domain abstractions");

    private static readonly IObjectProvider<IType> Mapper = Types()
        .That().HaveFullName("AutoMapper.IMapper")
        .As("the mapper");

    private static readonly IObjectProvider<Class> ApiServices = Classes()
        .That().HaveNameEndingWith("ApiService")
        .As("API services");

    private static readonly IObjectProvider<Class> DbContext = Classes()
        .That().HaveFullName("DM.Infrastructure.Persistence.DmDbContext")
        .As("the EF context");

    /// <summary>
    /// A rule that matches nothing passes, so the loader itself is asserted:
    /// a filter that stops resolving would otherwise turn both rules green.
    /// </summary>
    [Fact]
    public void LoadTheHostAndTheDomainAssemblies()
    {
        Controllers.GetObjects(Solution).Should().NotBeEmpty();
        DomainAbstractions.GetObjects(Solution).Should().NotBeEmpty();
        ApiServices.GetObjects(Solution).Should().NotBeEmpty();
        DbContext.GetObjects(Solution).Should().ContainSingle();
        Mapper.GetObjects(Solution).Should().ContainSingle();
    }

    [Fact]
    public void KeepControllersOffTheDomainAbstractions()
    {
        Classes().That().Are(Controllers)
            .Should().NotDependOnAny(DomainAbstractions)
            .Because("a controller talks to its ApiService, which owns the shape of the " +
                     "response; reaching past it puts mapping and envelope assembly in " +
                     "the action and couples the wire format to the domain signature")
            .Check(Solution);
    }

    /// <summary>
    /// The mechanical marker of the same bypass. Every controller that reached
    /// past its ApiService also held an IMapper, because mapping is the work it
    /// was doing in the action.
    /// </summary>
    [Fact]
    public void KeepControllersOffTheMapper()
    {
        Classes().That().Are(Controllers)
            .Should().NotDependOnAny(Mapper)
            .Because("turning a domain model into a DTO is the ApiService's job; a " +
                     "controller holding a mapper is one that has taken it over")
            .Check(Solution);
    }

    [Fact]
    public void KeepApiServicesOffTheDbContext()
    {
        Classes().That().Are(ApiServices)
            .Should().NotDependOnAny(DbContext)
            .Because("data access belongs to a repository behind a domain service; an " +
                     "API service that queries the context bypasses the authorization " +
                     "and invariants the domain layer exists to enforce")
            .Check(Solution);
    }
}
