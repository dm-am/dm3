using System;
using System.IO;
using System.Linq;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using FluentAssertions;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace DM.Architecture.Tests;

/// <summary>
/// The HTTP host talks to the stores through the domain, everywhere and not only
/// in its API services.
/// </summary>
/// <remarks>
/// The sibling rule names one shape — a class called *ApiService — and the host
/// is much more than those. Ten background jobs sat outside it: nine of them held
/// DmDbContext or DmMongoClient and wrote product rules inline, so the definition
/// of popularity, the lifetime of a token and the threshold for prodding a player
/// about an owed post existed only inside a running host. None could be called
/// from anywhere else, none could be covered by a domain test, and the seeder
/// ended up carrying its own copy of the popularity calculation with a window
/// that had already drifted from the site's.
///
/// The exemptions are the composition root and the warmup pass, and they are
/// named one by one rather than described by a pattern: bootstrap is the one job
/// whose work IS the storage type — a pool to open, a query plan to compile —
/// and a pattern would quietly readmit the next job that looked like it.
///
/// Read out of the IL like its neighbours, and through the same fluent rules: a
/// type reached through a using alias leaves nothing in the file for a textual
/// check to find, and a hand-walked dependency list is a check whose own
/// coverage nobody verifies.
/// </remarks>
public class HostStorageBoundaryShould
{
    private static readonly ArchitectureModel Solution = new ArchLoader()
        .LoadFilteredDirectory(AppContext.BaseDirectory, "DM.*.dll", SearchOption.TopDirectoryOnly)
        .Build();

    /// <summary>
    /// Types of the host allowed to name a store: the composition root, which
    /// builds the pool and applies the migration; the registration of the
    /// infrastructure initializers; and the warmup pass, whose entire job is to
    /// force EF and the driver to do their one-time work before a user waits on
    /// it.
    /// </summary>
    private static readonly string[] Bootstrap =
    [
        "Startup",
        "HostedServiceExtensions",
        "WarmupService",
    ];

    /// <summary>
    /// Classes outside the persistence project that derive the Mongo collection
    /// base, with the reason each is still there.
    /// </summary>
    /// <remarks>
    /// Both are notification senders of the dispatcher, and both read the same
    /// settings document through a view of their own. They are named here so the
    /// rule covers everything else: the HTTP host used to hold a third one, and
    /// nothing said a word about it.
    /// </remarks>
    private static readonly string[] MongoBaseOutsidePersistence =
    [
        "DM.Workers.NotificationDispatcher.Bot.NotificationBotSender",
        "DM.Workers.NotificationDispatcher.Email.NotificationEmailSender",
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
        .Or().HaveFullName("DM.Infrastructure.Persistence.MongoIntegration.DmMongoClient")
        .As("the store handles");

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
        HostClasses.GetObjects(Solution).Should().HaveCountGreaterThan(100,
            "the host is a whole application, and a filter that stopped matching would " +
            "leave every rule below green over nothing");

        HostClassesOutsideBootstrap.GetObjects(Solution).Should().HaveCountGreaterThan(100,
            "the exemption list names three types, so removing them must not empty the set");

        StoreHandles.GetObjects(Solution).Should().HaveCount(2,
            "both handles must be in the model; a rule whose target is absent passes " +
            "without checking anything");

        PersistenceEntities.GetObjects(Solution).Should().HaveCountGreaterThan(50,
            "the entity namespace holds the whole relational and document model");
    }

    [Fact]
    public void KeepTheWholeHostOffTheStoreHandles() =>
        Classes().That().Are(HostClassesOutsideBootstrap)
            .Should().NotDependOnAny(StoreHandles)
            .Because("a type of the host that holds the context or the Mongo client has " +
                     "taken over a decision the domain also makes, and the rule it writes " +
                     "inline cannot be called or tested without starting a host")
            .Check(Solution);

    [Fact]
    public void KeepTheWholeHostOffThePersistenceEntities() =>
        Classes().That().Are(HostClassesOutsideBootstrap)
            .Should().NotDependOnAny(PersistenceEntities)
            .Because("an EF or Mongo entity is the store's own shape; the host that maps " +
                     "one into a response has bound the wire format to the table, and the " +
                     "settings document it did that with was read and rewritten whole on " +
                     "every call")
            .Check(Solution);

    /// <summary>
    /// No source file of the host names the persistence project at all, the three
    /// bootstrap files aside.
    /// </summary>
    /// <remarks>
    /// This is the rule that actually holds, and the two above are the belt to its
    /// braces. Measured, not assumed: a class that takes DmDbContext in a method
    /// signature is caught by them, and the same class using it inside an `async`
    /// method body is not — the compiler moves that body into a generated state
    /// machine, and the loader does not put generated types in the model, so the
    /// dependency belongs to a type no rule can see. Nearly all data access here
    /// is written inside async methods, which is how nine background jobs held the
    /// context while the IL rule next door reported green.
    ///
    /// Matching on the namespace rather than on type names, because an alias
    /// spells the namespace it aliases: `using DbChat = DM.Infrastructure...` is
    /// the shape the host actually used, and it leaves the namespace in the file
    /// either way.
    /// </remarks>
    [Fact]
    public void NameThePersistenceProjectInNoSourceFileButBootstrap()
    {
        var root = RepositoryRoot;
        var host = Path.Combine(root, "src", "DM.Web.API");

        var sources = Directory
            .GetFiles(host, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "obj" or "bin"))
            .ToList();

        sources.Should().HaveCountGreaterThan(200,
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

    /// <summary>
    /// The Mongo collection base belongs to the persistence project. A class
    /// elsewhere that derives it is a repository living outside the layer that
    /// owns repositories, with its own idea of the document it shares.
    /// </summary>
    [Fact]
    public void LeaveTheMongoCollectionBaseToThePersistenceProject()
    {
        var derived = Solution.Classes
            .Where(c => c.BaseClass != null &&
                        c.BaseClass.Name.StartsWith("MongoCollectionRepository", StringComparison.Ordinal))
            .ToList();

        derived.Should().NotBeEmpty(
            "the persistence project derives this base several times over, and finding " +
            "none means the rule matches on a name that no longer exists");

        var offenders = derived
            .Where(c => !c.Assembly.Name.StartsWith("DM.Infrastructure.Persistence", StringComparison.Ordinal))
            .Select(c => c.FullName)
            .Where(name => !MongoBaseOutsidePersistence.Any(known =>
                name.StartsWith(known, StringComparison.Ordinal)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a collection is one shape, and every class that opens it with a view of its " +
            "own is free to disagree with the others about what the document holds");
    }

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
