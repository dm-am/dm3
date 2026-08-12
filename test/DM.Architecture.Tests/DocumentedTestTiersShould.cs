using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The testing guide names every runner this tree tests with.
/// </summary>
/// <remarks>
/// A tier the guide does not mention is a tier nobody runs before pushing and
/// nobody updates after changing a screen. The browser tier lived exactly like
/// that: its own spec corpus, four package scripts and a job both publish jobs
/// depend on, against a guide that described the two tiers which existed when it
/// was written. A contributor learned that the tier existed from a red pipeline,
/// and the quieter half of the cost is a screen changed without its spec.
///
/// The runners are read out of the package scripts and the test projects instead
/// of being listed here, so a runner added to the tree turns this red until the
/// guide has a line for it. A tier is not a runner: the backend unit, integration
/// and architecture projects all reference xunit and collapse to one word here, so
/// a fourth xunit tier passes this silently. Naming the tiers is on the guide and
/// on review, and this gate only holds the runner names.
/// </remarks>
public class DocumentedTestTiersShould
{
    /// <summary>What can stand in front of the runner on a script line.</summary>
    private static readonly string[] Wrappers =
        ["npx", "npm", "run", "run-p", "run-s", "cross-env"];

    /// <summary>The .NET test frameworks a test project can be built on.</summary>
    private static readonly Regex TestFramework = new(
        @"PackageReference Include=""(xunit|nunit|mstest)""", RegexOptions.Compiled);

    private static DirectoryInfo RepositoryRoot => DM.Testing.RepositoryLayout.RootDirectory;

    [Fact]
    public void NameEveryRunnerTheTreeTestsWith()
    {
        var root = RepositoryRoot;
        var guide = File.ReadAllText(
            Path.Combine(root.FullName, "docs", "guides", "TESTING.md"));

        var runners = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        using var manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root.FullName, "src", "DM.Web.Client", "package.json")));

        manifest.RootElement.TryGetProperty("scripts", out var scripts)
            .Should().BeTrue("the client declares its commands as npm scripts");

        foreach (var script in scripts.EnumerateObject()
                     .Where(script => script.Name.StartsWith("test", StringComparison.Ordinal)))
        {
            var runner = (script.Value.GetString() ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(word => !Wrappers.Contains(word, StringComparer.Ordinal));

            if (runner != null)
            {
                runners.Add(runner);
            }
        }

        var projects = Directory.GetFiles(
            Path.Combine(root.FullName, "test"), "*.csproj", SearchOption.AllDirectories);

        projects.Should().NotBeEmpty("the backend tests live under test/");

        foreach (var project in projects)
        {
            foreach (Match reference in TestFramework.Matches(File.ReadAllText(project)))
            {
                runners.Add(reference.Groups[1].Value);
            }
        }

        runners.Should().HaveCountGreaterThan(
            2, "this tree has a backend tier, a frontend unit tier and a browser tier");

        var unnamed = runners
            .Where(runner => !guide.Contains(runner, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        unnamed.Should().BeEmpty(
            "a tier the guide does not name is a tier the reader does not know to run, and the " +
            "one that stayed unnamed only ever ran on the owner's machine");
    }
}
