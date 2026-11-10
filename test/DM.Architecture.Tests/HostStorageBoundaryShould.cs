using System;
using System.IO;
using System.Linq;
using ArchUnitNET.Domain;
using ArchUnitNET.xUnitV3;
using AwesomeAssertions;
using DM.Testing;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace DM.Architecture.Tests;

/// <remarks>
/// The sibling rule names *ApiService only. The bootstrap exemptions are named one by one. Read out
/// of the IL: a type reached through a using alias leaves nothing in the file.
/// </remarks>
public class HostStorageBoundaryShould
{
    private static readonly ArchitectureModel Solution = ArchitectureSolution.Model;

    /// <summary>
    /// Types of the host allowed to name a store: the composition root, the registration of the
    /// infrastructure initializers and the warmup pass.
    /// </summary>
    private static readonly string[] Bootstrap =
    [
        "Startup",
        "HostedServiceExtensions",
        "WarmupService",
    ];

    private static bool IsHost(IType type) =>
        type.Assembly.Name.StartsWith("DM.Web.API", StringComparison.Ordinal);

    private static readonly IObjectProvider<Class> HostClasses = Classes()
        .That().FollowCustomPredicate(IsHost, "are declared in the HTTP host")
        .As("host classes");

    private static readonly IObjectProvider<Class> HostClassesOutsideBootstrap = Classes()
        .That().FollowCustomPredicate(
            c => IsHost(c) && !Bootstrap.Contains(c.Name, StringComparer.Ordinal),
            "are declared in the HTTP host outside its bootstrap")
        .As("host classes outside bootstrap");

    private static readonly IObjectProvider<IType> StoreHandles = Types()
        .That().HaveFullName("DM.Infrastructure.Persistence.DmDbContext")
        .As("the store handle");

    private static readonly IObjectProvider<IType> PersistenceEntities = Types()
        .That().FollowCustomPredicate(
            t => t.Namespace.FullName.StartsWith("DM.Infrastructure.Persistence.Entities", StringComparison.Ordinal),
            "are declared under the persistence entity namespace")
        .As("the persistence entities");

    /// <summary>
    /// A rule over an empty set passes, so what the loader found is asserted
    /// before anything is asked of it.
    /// </summary>
    [Fact]
    public void LoadTheHostAndEverythingTheRulesNameBelow()
    {
        HostClasses.GetObjects(Solution).Should().NotBeEmpty(
            "the host is a whole application, and a filter that stopped matching would " +
            "leave every rule below green over nothing");

        HostClassesOutsideBootstrap.GetObjects(Solution).Should().NotBeEmpty(
            "the exemption list names three types, so removing them must not empty the set");

        StoreHandles.GetObjects(Solution).Should().HaveCount(1,
            "the handle must be in the model; a rule whose target is absent passes " +
            "without checking anything");

        PersistenceEntities.GetObjects(Solution).Should().NotBeEmpty(
            "the entity namespace holds the whole relational model");
    }

    [Fact]
    public void KeepTheWholeHostOffTheStoreHandles() =>
        Classes().That().Are(HostClassesOutsideBootstrap)
            .Should().NotDependOnAny(StoreHandles)
            .Because("a type of the host that holds the context has " +
                     "taken over a decision the domain also makes, and the rule it writes " +
                     "inline cannot be called or tested without starting a host")
            .Check(Solution);

    [Fact]
    public void KeepTheWholeHostOffThePersistenceEntities() =>
        Classes().That().Are(HostClassesOutsideBootstrap)
            .Should().NotDependOnAny(PersistenceEntities)
            .Because("an EF entity is the store's own shape; the host that maps " +
                     "one into a response has bound the wire format to the table, and the " +
                     "settings document it did that with was read and rewritten whole on " +
                     "every call")
            .Check(Solution);

    /// <remarks>
    /// The loader does not model the state machine of an async body, so the IL rules above miss a store
    /// used inside one. Matched on the namespace, which an alias spells too.
    /// </remarks>
    [Fact]
    public void NameThePersistenceProjectInNoSourceFileButBootstrap()
    {
        var root = RepositoryLayout.Root;
        var host = Path.Combine(root, "src", "DM.Web.API");

        var sources = Directory
            .GetFiles(host, "*.cs", SearchOption.AllDirectories)
            .Where(RepositoryFiles.IsAuthored)
            .ToList();

        sources.Should().NotBeEmpty(
            "the sources are discovered from the tree, and a path that stopped matching " +
            "would leave this rule reading nothing");

        var offenders = sources
            .Where(path => !Bootstrap.Contains(
                Path.GetFileNameWithoutExtension(path), StringComparer.Ordinal))
            .Where(path => File.ReadAllText(path)
                .Contains("DM.Infrastructure.Persistence", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "the host states what it needs and the domain answers; a file of the host " +
            "that reaches into the store writes a rule only a running host can execute, " +
            "and the definition of popularity, the lifetime of a token and the threshold " +
            "for prodding a player all lived there");
    }
}
