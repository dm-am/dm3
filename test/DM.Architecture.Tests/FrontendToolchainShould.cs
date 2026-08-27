using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The Node that writes the lock file is the Node that reads it back.
/// </summary>
/// <remarks>
/// npm is not installed next to Node, it is installed inside it: a Node release
/// carries one npm and no other. So the Node version is the npm version, and the
/// npm version decides whether package-lock.json is accepted or refused - `npm ci`
/// rejects a file whose shape its own npm would not have written.
///
/// Nothing pinned it. The workflow asked setup-node for '24' three times, the SPA
/// image built on node:24-alpine, and package.json named no version at all, so the
/// lock file was written by the 11.6.2 that ships in 24.13.0 and read in CI by the
/// 11.17.0 that had arrived in a later patch of the same major. Three jobs went
/// red on a file nobody had touched.
///
/// This is the rule CompilerPolicyShould.PinTheSdkToTheBandContinuousIntegrationInstalls
/// already writes for the .NET SDK, with one difference that is the whole point:
/// the SDK is pinned to a band because a band is what setup-dotnet installs and
/// what the images carry. Here the divergence happened inside the band, so the pin
/// is exact down to the patch.
///
/// package.json is the source of truth because it is the file npm itself reads -
/// engines is checked on every install and warns when the machine disagrees. The
/// workflow and the Dockerfile are held to it rather than to a number written
/// here, so this rule cannot go stale on its own.
/// </remarks>
public class FrontendToolchainShould
{
    /// <summary>A node-version a workflow asks setup-node to install.</summary>
    private static readonly Regex NodeVersion = new(
        @"node-version:\s*'?(?<version>[^'\s]+)'?", RegexOptions.Compiled);

    /// <summary>The tag of a Node base image, without the variant suffix.</summary>
    private static readonly Regex NodeImage = new(
        @"FROM\s+node:(?<version>[0-9][^\s-]*)", RegexOptions.Compiled);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string ClientPath(string file) =>
        Path.Combine(RepositoryRoot, "src", "DM.Web.Client", file);

    [Fact]
    public void InstallTheSameNodeEverywhereItIsInstalled()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(ClientPath("package.json")));

        manifest.RootElement.TryGetProperty("engines", out var engines).Should().BeTrue(
            "package.json is the file npm reads, so it is where the version of the toolchain " +
            "is decided; without engines nothing in this repository names one at all");

        var pinned = engines.GetProperty("node").GetString()!;

        // A range is what was there in spirit before: '24' accepts every patch of
        // the major, and the patch is what carries npm.
        pinned.Should().MatchRegex(@"^[0-9]+\.[0-9]+\.[0-9]+$",
            "npm ships inside the Node release, so a range hands the lock file to whichever " +
            "npm the newest patch happens to carry - see the class remarks");

        var declarations = new List<(string Where, string Version)>();

        foreach (var workflow in Directory.EnumerateFiles(
                     Path.Combine(RepositoryRoot, ".github", "workflows"), "*.yml"))
        {
            declarations.AddRange(NodeVersion
                .Matches(File.ReadAllText(workflow))
                .Select(match => (
                    $".github/workflows/{Path.GetFileName(workflow)}",
                    match.Groups["version"].Value)));
        }

        declarations.AddRange(NodeImage
            .Matches(File.ReadAllText(ClientPath("Dockerfile")))
            .Select(match => (
                "src/DM.Web.Client/Dockerfile",
                match.Groups["version"].Value)));

        // Each source named separately, because a rule that stops matching passes
        // in silence: a renamed key or a rewritten FROM line would leave this
        // comparing one place against itself and reporting agreement.
        declarations.Should().Contain(
            declaration => declaration.Where.StartsWith(".github/", StringComparison.Ordinal),
            "the workflows install Node before they touch the lock file");
        declarations.Should().Contain(
            declaration => declaration.Where.EndsWith("Dockerfile", StringComparison.Ordinal),
            "the SPA image is built on a Node base image, and it runs npm ci too");

        var divergent = declarations
            .Where(declaration => declaration.Version != pinned)
            .Select(declaration => $"{declaration.Where}: {declaration.Version}")
            .Distinct()
            .OrderBy(offender => offender, StringComparer.Ordinal)
            .ToArray();

        // Joined rather than asserted as a collection: an empty-collection failure
        // prints the first item, and the whole point of this list is that it names
        // every place that has drifted, not the first one somebody has to fix
        // before seeing the next.
        string.Join("; ", divergent).Should().BeEmpty(
            "package.json pins Node {0}, and these install another one, so the lock file is " +
            "written by one npm and read by a different one",
            pinned);
    }

    /// <summary>
    /// The npm named next to it is the npm that Node actually carries.
    /// </summary>
    /// <remarks>
    /// engines.npm is what makes the warning legible: without it npm reports
    /// nothing when the machine drifts, and the first sign is a lock file CI
    /// refuses. It is only worth writing while it names the npm bundled in the
    /// pinned Node - a second number to keep in step, and one that cannot be
    /// derived from anything in this tree, so what is asserted is that it exists
    /// and is exact rather than which value it holds.
    /// </remarks>
    [Fact]
    public void NameTheNpmThatComesWithThatNode()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(ClientPath("package.json")));

        var engines = manifest.RootElement.GetProperty("engines");
        engines.TryGetProperty("npm", out var npm).Should().BeTrue(
            "the npm is the half of the toolchain that reads package-lock.json, and a machine " +
            "that quietly runs another one is what put three jobs in the red");

        npm.GetString().Should().MatchRegex(@"^[0-9]+\.[0-9]+\.[0-9]+$",
            "a range here accepts the very drift the pin exists against");
    }
}
