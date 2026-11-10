using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// A task nobody awaits outlives the request scope and publishes through a disposed sender, or leaks
/// the channel <see cref="MessageProducerOwnershipShould"/> guards. Hosted services are exempt.
/// </remarks>
public class RequestPathBackgroundWorkShould
{
    private static string SourceDirectory => Path.Combine(DM.Testing.RepositoryLayout.Root, "src");

    private static readonly (string Name, Regex Pattern)[] Handoffs =
    [
        ("Task.Run", new Regex(@"Task\.Run\s*\(", RegexOptions.Compiled)),
        ("Task.Factory.StartNew", new Regex(@"Task\.Factory\.StartNew\s*\(", RegexOptions.Compiled)),
        ("ContinueWith", new Regex(@"\.ContinueWith\s*\(", RegexOptions.Compiled)),
        ("async void", new Regex(@"\basync\s+void\b", RegexOptions.Compiled)),
        ("a discarded call", new Regex(@"(?<![\w.])_\s*=\s*[A-Za-z_][\w.]*\s*\(", RegexOptions.Compiled)),
    ];

    // Files whose lifetime is the process, not a scope.
    private static bool IsHostedService(string code) =>
        code.Contains("IHostedService", StringComparison.Ordinal) ||
        code.Contains("BackgroundService", StringComparison.Ordinal);

    private static IEnumerable<(string Path, string Code)> Sources() => Directory
        .EnumerateFiles(SourceDirectory, "*.cs", SearchOption.AllDirectories)
        .Where(RepositoryFiles.IsAuthored)
        .Select(path => (path, SourceText.ReadCode(path)));

    [Fact]
    public void RefuseBackgroundWorkOnThePathOfARequest()
    {
        var offenders = Sources()
            .Where(source => !IsHostedService(source.Code))
            .SelectMany(source => Handoffs
                .Where(handoff => handoff.Pattern.IsMatch(source.Code))
                .Select(handoff => $"{Path.GetFileName(source.Path)} hands work off with {handoff.Name}"))
            .ToList();

        offenders.Should().BeEmpty(
            "a task nobody waits for outlives the scope that owns its AMQP channel and its " +
            "database connection, and the only thing that ever reports the failure is the source");
    }

    /// <summary>
    /// A rule that scanned nothing would pass, and so would one whose exemption had
    /// grown wide enough to cover the request path.
    /// </summary>
    [Fact]
    public void FindTheTreeAndTheOneExemption()
    {
        var sources = Sources().ToList();
        sources.Should().NotBeEmpty("the walk must reach the sources");

        var exempt = sources
            .Where(source => IsHostedService(source.Code))
            .Where(source => Handoffs.Any(handoff => handoff.Pattern.IsMatch(source.Code)))
            .Select(source => Path.GetFileName(source.Path))
            .ToList();

        exempt.Should().BeEquivalentTo(["WarmupService.cs"],
            "the exemption is for lifetimes measured in processes; anything else on this " +
            "list is a scope-bound path that called itself a hosted service");
    }
}
