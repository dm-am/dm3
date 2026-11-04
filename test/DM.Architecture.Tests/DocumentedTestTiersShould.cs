using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The testing guide names every runner this tree tests with.
/// </summary>
/// <remarks>
/// The runners are read out of the package scripts and the test projects. A tier is not a runner:
/// the xunit projects collapse to one word here.
/// </remarks>
public class DocumentedTestTiersShould
{
    /// <summary>What can stand in front of the runner on a script line.</summary>
    private static readonly string[] Wrappers =
        ["npx", "npm", "run", "run-p", "run-s", "cross-env"];

    /// <summary>The .NET test frameworks a test project can be built on.</summary>
    /// <remarks>The xunit.v3 suffix is dropped: the guide names the runner, not the package id.</remarks>
    private static readonly Regex TestFramework = new(
        @"PackageReference Include=""(?<runner>xunit|nunit|mstest)(?:\.v3)?""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);


    [Fact]
    public void NameEveryRunnerTheTreeTestsWith()
    {
        var root = RepositoryLayout.RootDirectory;
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
                runners.Add(reference.Groups["runner"].Value);
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
