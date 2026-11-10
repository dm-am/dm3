using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Matched on source text: one rule names a shape (I*Repository), not a type, and the IL rules
/// do not see a type used only inside an async body. See ServiceLayerBoundaryShould.
/// </remarks>
public class ApiServiceBoundaryShould
{
    /// <summary>
    /// Uploads have no owning module, so there is no domain service to call; closing that needs a
    /// new module. The exemption names dependencies, not the file.
    /// </summary>
    private static readonly Dictionary<string, string[]> WithoutADomainOwner = new()
    {
        ["UploadApiService.cs"] = ["IUploadRepository", "IIntentionManager"]
    };

    // The namespace qualifier is optional: written in full, a type would not start the match at the
    // I.
    private static readonly Regex ForbiddenDependency = new(
        @"^[ \t]*private readonly (?:[A-Za-z0-9_]+\.)*(?<name>I[A-Za-z0-9]*Repository|IIntentionManager)\s",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex InjectedDependency = new(
        @"^[ \t]*private readonly (?:[A-Za-z0-9_]+\.)*(?<name>I[A-Za-z0-9]*)\s",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex ImplementedContract = new(
        @"\bclass\s+\w+\s*:\s*(?<bases>[^{]+)", RegexOptions.Compiled);

    // The parts of a partial service count too: a field may be declared in any part.
    private static IReadOnlyList<string> Sources(params string[] patterns) => patterns
        .SelectMany(pattern => Directory.GetFiles(
            Path.Combine(RepositoryLayout.Root, "src", "DM.Web.API"), pattern, SearchOption.AllDirectories))
        .Where(RepositoryFiles.IsAuthored)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToList();

    private static List<string> Offenders(Regex dependencies, Func<string, bool> forbidden) =>
        Sources("*ApiService.cs", "*ApiService.*.cs")
            .SelectMany(path => dependencies.Matches(File.ReadAllText(path))
                .Select(match => match.Groups["name"].Value)
                .Where(forbidden)
                .Where(dependency => !(WithoutADomainOwner.TryGetValue(Path.GetFileName(path), out var allowed)
                                       && allowed.Contains(dependency)))
                .Select(dependency => RepositoryFiles.Relative(path) + " -> " + dependency))
            .ToList();

    /// <summary>
    /// A rule over an empty set passes, and this set is discovered from the tree, so
    /// a pattern that stopped matching would leave the rule below green.
    /// </summary>
    [Fact]
    public void FindEveryApiServiceOfTheHost() =>
        Sources("*ApiService.cs").Should().NotBeEmpty(
            "the host declares an API service per feature, and a rule that found none " +
            "would be green for that reason alone");

    [Fact]
    public void BeBuiltOutOfDomainServicesAndDecideNoPermission() =>
        Offenders(ForbiddenDependency, _ => true).Should().BeEmpty(
            "the domain service owns the rules and the repository behind them; an API " +
            "service holding either has taken over a decision the domain also makes, and " +
            "the two only have to agree until they stop");

    /// <remarks>
    /// The rule above reads names, and a data-access contract not called I*Repository walks past
    /// it; where it is implemented is the evidence.
    /// </remarks>
    [Fact]
    public void HoldNoContractThePersistenceLayerImplementsWhateverItIsNamed()
    {
        var repositories = Path.Combine(RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence", "Repositories");
        var implemented = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in Directory
                     .GetFiles(repositories, "*.cs", SearchOption.AllDirectories)
                     .Where(RepositoryFiles.IsAuthored))
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

        Offenders(InjectedDependency, implemented.Contains).Should().BeEmpty(
            "a contract the persistence layer implements is a repository however it is " +
            "named, and an API service holding one has skipped the domain service that " +
            "owns the rules over it");
    }
}
