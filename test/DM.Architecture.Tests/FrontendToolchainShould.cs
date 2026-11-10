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

/// <remarks>
/// A Node release carries one npm, and `npm ci` refuses a lock file its npm would not have written,
/// so the pin is exact to the patch. package.json is the source; the workflow and the Dockerfile
/// are held to it.
/// </remarks>
public class FrontendToolchainShould
{
    private static readonly Regex NodeVersion = new(
        @"node-version:\s*'?(?<version>[^'\s]+)'?", RegexOptions.Compiled);

    private static readonly Regex NodeImage = new(
        @"FROM\s+node:(?<version>[0-9][^\s-]*)", RegexOptions.Compiled);


    private static string ClientPath(string file) =>
        Path.Combine(RepositoryLayout.Root, "src", "DM.Web.Client", file);

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
                     Path.Combine(RepositoryLayout.Root, ".github", "workflows"), "*.yml"))
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

        // Each source named separately: a rule that stops matching passes in silence.
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

        // Joined, so the message names every place that has drifted.
        string.Join("; ", divergent).Should().BeEmpty(
            "package.json pins Node {0}, and these install another one, so the lock file is " +
            "written by one npm and read by a different one",
            pinned);
    }

    // Without engines.npm npm reports nothing when the machine drifts; its value cannot be derived
    // here.
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
