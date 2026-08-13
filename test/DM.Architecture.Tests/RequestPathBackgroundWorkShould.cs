using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Work started on the path of a request has to finish on it.
/// </summary>
/// <remarks>
/// Everything the request holds is scoped to the request: the database context,
/// the identity, and — the one that bites — the mail sender, which takes an AMQP
/// channel out of the pool and gives it back in Dispose. A task handed off with
/// no one waiting for it outlives that scope with nothing ordering the two, so
/// the losing side of the race publishes through a disposed sender. Either it
/// throws where nobody is listening, or it sends Jamq to retake a channel that
/// nobody is left to return — the pool exhaustion that
/// <see cref="MessageProducerOwnershipShould"/> exists to prevent, arriving by a
/// different door.
///
/// The defect is invisible in every other way. The abandoned task swallows its
/// own exception, the request answers normally, and the letter that never left
/// looks exactly like a letter nobody needed. Only the source says it happened.
///
/// Hosted services are exempt and only they are: outliving the caller is what
/// they are for, and their lifetime is the process rather than a scope.
/// </remarks>
public class RequestPathBackgroundWorkShould
{
    private static string SourceDirectory => Path.Combine(DM.Testing.RepositoryLayout.Root, "src");

    /// <summary>
    /// Handing work off with nobody waiting for it, in every spelling the language
    /// offers. The last one is the cheapest and the easiest to write by accident:
    /// a discarded task is a fire-and-forget with no ceremony around it.
    /// </summary>
    private static readonly (string Name, Regex Pattern)[] Handoffs =
    [
        ("Task.Run", new Regex(@"Task\.Run\s*\(", RegexOptions.Compiled)),
        ("Task.Factory.StartNew", new Regex(@"Task\.Factory\.StartNew\s*\(", RegexOptions.Compiled)),
        ("ContinueWith", new Regex(@"\.ContinueWith\s*\(", RegexOptions.Compiled)),
        ("async void", new Regex(@"\basync\s+void\b", RegexOptions.Compiled)),
        ("a discarded call", new Regex(@"(?<![\w.])_\s*=\s*[A-Za-z_][\w.]*\s*\(", RegexOptions.Compiled)),
    ];

    private static bool IsAuthored(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>Files whose lifetime is the process, not a scope.</summary>
    private static bool IsHostedService(string code) =>
        code.Contains("IHostedService", StringComparison.Ordinal) ||
        code.Contains("BackgroundService", StringComparison.Ordinal);

    private static IEnumerable<(string Path, string Code)> Sources() => Directory
        .EnumerateFiles(SourceDirectory, "*.cs", SearchOption.AllDirectories)
        .Where(IsAuthored)
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
        sources.Should().HaveCountGreaterThan(500, "the walk must reach the sources");

        var exempt = sources
            .Where(source => IsHostedService(source.Code))
            .Where(source => Handoffs.Any(handoff => handoff.Pattern.IsMatch(source.Code)))
            .Select(source => Path.GetFileName(source.Path))
            .ToList();

        exempt.Should().BeEquivalentTo(["WarmupService.cs"],
            "the exemption is for lifetimes measured in processes; anything else on this " +
            "list is a scope-bound path that called itself a hosted service");
    }

    /// <summary>
    /// The detector is text, and a typo in it reads exactly like a clean tree.
    /// </summary>
    [Fact]
    public void DetectTheShapeItLooksFor()
    {
        var detected = (string line) => Handoffs.Any(handoff => handoff.Pattern.IsMatch(line));

        detected("_ = Task.Run(async () => await Send());").Should().BeTrue();
        detected("_ = sender.SendAsync(email);").Should().BeTrue();
        detected("await sender.SendAsync(email);").Should().BeFalse();
        detected("var _unused = Compute();").Should().BeFalse();
    }
}
