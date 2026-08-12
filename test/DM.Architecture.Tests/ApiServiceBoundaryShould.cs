using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// What the API layer is built out of, and the one number its sibling rule cites.
/// </summary>
/// <remarks>
/// The rules next door read the IL, which is the stronger check for a type reached
/// through an alias. These cannot: one asserts a number written in a comment, and
/// the other names the shape of a dependency (anything called I*Repository) rather
/// than a single type, so the name is what there is to match on. Stronger there is
/// not stronger everywhere: the IL rules do not see a type used only inside an
/// async method body, which is where nearly all data access is written — see the
/// remarks on ServiceLayerBoundaryShould.
///
/// They close the same gap. KeepApiServicesOffTheDbContext names one class, so an
/// API service that skipped its domain service by holding the repository behind it
/// passed the suite: six did, and two of them decided permissions as well. The chat
/// of a game room is what that cost. The API service authorized by the room's
/// access policy while the chat service below authorized by membership of the chat,
/// membership is never created for a room chat, and every read and every write
/// answered 404 to everybody, the game master included. Two answers to one question
/// agree until the day they do not, and nothing between them says which is meant.
/// </remarks>
public class ApiServiceBoundaryShould
{
    /// <summary>
    /// Uploads are the one feature with no owning module: every contract they need
    /// is declared in the kernel and implemented by the persistence layer, so there
    /// is no domain service for this one to call, and the kernel may not hold one --
    /// it is the project nothing else may be referenced from. Closing the gap means
    /// a new domain module, which is the owner's decision and not this rule's.
    /// </summary>
    /// <remarks>
    /// Named dependencies rather than a named file. Skipping the file put the whole
    /// of UploadApiService outside the rule, so a sixth repository added to it would
    /// have been as invisible as the one this list is about: the exemption covers
    /// what the finding measured and nothing more, and a new entry here has to be
    /// argued the same way.
    /// </remarks>
    private static readonly Dictionary<string, string[]> WithoutADomainOwner = new()
    {
        ["UploadApiService.cs"] = ["IUploadRepository", "IIntentionManager"]
    };

    /// <summary>
    /// A dependency an API service is not allowed to be built out of.
    /// </summary>
    /// <remarks>
    /// The namespace qualifier is optional and discarded: written out in full, a
    /// type does not start the match at the I and the field reads as ordinary.
    /// </remarks>
    private static readonly Regex ForbiddenDependency = new(
        @"^[ \t]*private readonly (?:[A-Za-z0-9_]+\.)*(?<name>I[A-Za-z0-9]*Repository|IIntentionManager)\s",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>Any dependency an API service holds, whatever it is called.</summary>
    private static readonly Regex InjectedDependency = new(
        @"^[ \t]*private readonly (?:[A-Za-z0-9_]+\.)*(?<name>I[A-Za-z0-9]*)\s",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>An interface a class in the persistence layer implements.</summary>
    private static readonly Regex ImplementedContract = new(
        @"\bclass\s+\w+\s*:\s*(?<bases>[^{]+)", RegexOptions.Compiled);

    /// <summary>An import of a domain module's feature namespace.</summary>
    private static readonly Regex DomainFeatureImport = new(
        @"^using DM\.Domain\.[A-Za-z0-9]+\.Features\.",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>The count the assembly-match rule cites for choosing its shape.</summary>
    private static readonly Regex CitedControllerCount = new(
        @"namespace: (?<count>\d+) compliant", RegexOptions.Compiled);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static IReadOnlyList<string> Sources(string root, string pattern) => Directory
        .GetFiles(Path.Combine(root, "src", "DM.Web.API"), pattern, SearchOption.AllDirectories)
        .Where(IsAuthored)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// A rule over an empty set passes, and this set is discovered from the tree, so
    /// a pattern that stopped matching would leave the rule below green.
    /// </summary>
    [Fact]
    public void FindEveryApiServiceOfTheHost() =>
        Sources(RepositoryRoot, "*ApiService.cs").Should().HaveCountGreaterOrEqualTo(75,
            "the host declares an interface and a class per feature, and a rule that " +
            "found neither would be green for that reason alone");

    [Fact]
    public void BeBuiltOutOfDomainServicesAndDecideNoPermission()
    {
        var root = RepositoryRoot;
        var offenders = new List<string>();

        foreach (var path in Sources(root, "*ApiService.cs"))
        {
            WithoutADomainOwner.TryGetValue(Path.GetFileName(path), out var allowed);

            foreach (Match match in ForbiddenDependency.Matches(File.ReadAllText(path)))
            {
                var dependency = match.Groups["name"].Value;
                if (allowed != null && allowed.Contains(dependency))
                {
                    continue;
                }

                offenders.Add(Path.GetRelativePath(root, path) + " -> " + dependency);
            }
        }

        offenders.Should().BeEmpty(
            "the domain service owns the rules and the repository behind them; an API " +
            "service holding either has taken over a decision the domain also makes, and " +
            "the two only have to agree until they stop");
    }

    /// <summary>
    /// The number the assembly-match rule cites is the count of controllers importing
    /// a domain feature namespace, and it is the part of that comment which goes
    /// stale without anybody touching it: it said five while there were six.
    /// </summary>
    [Fact]
    public void CountTheControllersTheAssemblyMatchExempts()
    {
        var root = RepositoryRoot;
        var rule = File.ReadAllText(Path.Combine(
            root, "test", "DM.Architecture.Tests", "ServiceLayerBoundaryShould.cs"));

        var cited = CitedControllerCount.Match(rule);
        cited.Success.Should().BeTrue("the rule states in figures why it matches on the assembly");

        var importing = Sources(root, "*Controller.cs")
            .Where(path => DomainFeatureImport.IsMatch(File.ReadAllText(path)))
            .ToList();

        importing.Should().NotBeEmpty(
            "the exemption exists because such controllers do, and none of them would " +
            "mean the rule could match on the namespace after all");
        int.Parse(cited.Groups["count"].Value, CultureInfo.InvariantCulture).Should()
            .Be(importing.Count,
                "a number in a comment is a claim about the tree, and this one was out of " +
                "date before anybody read it again");
    }

    /// <summary>
    /// The same boundary, matched on what a dependency is rather than on what it
    /// is called.
    /// </summary>
    /// <remarks>
    /// The rule above reads the name because a name is what a source-level match
    /// has, and a data-access contract that is not called I*Repository walks past
    /// it. One did: a CRUD contract of one write and five reads, implemented by a
    /// class named SecurityAuditRepository and registered as that, was declared
    /// under the *Service suffix, so an API service held the repository directly
    /// and the suite stayed green. Renaming it closed that instance; this closes
    /// the class of it, because the next such contract will be named by whoever
    /// writes it.
    ///
    /// Implementation location, not the interface's name, is the evidence: the
    /// persistence layer is where a repository lives, and a contract it
    /// implements is one whatever the declaration is called.
    /// </remarks>
    [Fact]
    public void HoldNoContractThePersistenceLayerImplementsWhateverItIsNamed()
    {
        var root = RepositoryRoot;
        var repositories = Path.Combine(root, "src", "DM.Infrastructure.Persistence", "Repositories");
        var implemented = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in Directory
                     .GetFiles(repositories, "*.cs", SearchOption.AllDirectories)
                     .Where(IsAuthored))
        {
            foreach (Match match in ImplementedContract.Matches(File.ReadAllText(path)))
            {
                foreach (var baseType in match.Groups["bases"].Value.Split(','))
                {
                    var name = baseType.Trim().Split('<')[0].Trim();
                    if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
                    {
                        implemented.Add(name);
                    }
                }
            }
        }

        implemented.Should().NotBeEmpty("the persistence layer implements the contracts it exists for");

        var offenders = new List<string>();
        foreach (var path in Sources(root, "*ApiService.cs"))
        {
            WithoutADomainOwner.TryGetValue(Path.GetFileName(path), out var allowed);

            foreach (Match match in InjectedDependency.Matches(File.ReadAllText(path)))
            {
                var dependency = match.Groups["name"].Value;
                if (!implemented.Contains(dependency) || allowed?.Contains(dependency) == true)
                {
                    continue;
                }

                offenders.Add(Path.GetRelativePath(root, path) + " -> " + dependency);
            }
        }

        offenders.Should().BeEmpty(
            "a contract the persistence layer implements is a repository however it is " +
            "named, and an API service holding one has skipped the domain service that " +
            "owns the rules over it");
    }

    private static bool IsAuthored(string path) =>
        !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin");
}
