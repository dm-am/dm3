using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A counter that goes up on a live row comes back down on a row that may not be.
/// </summary>
/// <remarks>
/// The post count of an author is a column and not a view: it is raised when a
/// post is written and lowered when one is removed, and nothing recomputes it.
/// The soft-delete filter is global, so a set-based update reaches only rows that
/// are not removed — which is right for the write that raises it, since a
/// deactivated account writes no posts, and wrong for the one that lowers it,
/// since moderation removes the posts of deactivated accounts as a matter of
/// course.
///
/// Nothing fails when that happens. The update matches no rows and reports
/// success, the transaction commits, the post is gone, and the number on the
/// profile keeps counting it — with no way left to reconcile the two, because the
/// row that would have said what to subtract is the one that was deleted.
///
/// Asserted on the text of the statement because the provider that runs these is
/// the one thing a unit test cannot stand in for: ExecuteUpdate has no in-memory
/// implementation, so the only cheap place to see the filter is where it is
/// written.
/// </remarks>
public class CounterReachShould
{
    /// <summary>
    /// Columns that count something countable elsewhere, so a write that misses
    /// leaves two numbers that disagree with nothing to arbitrate between them.
    /// </summary>
    private static readonly string[] Counters = ["QuantityRating"];

    /// <summary>
    /// One set-based write of a counter: everything from the set it starts at to
    /// the update that ends it.
    /// </summary>
    private static readonly Regex Update = new(
        @"_dbContext\.\w+(?<between>(?:\s*\.\w+\([^;]*?)?)\.ExecuteUpdateAsync\((?<body>[^;]*?)\);",
        RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void LowerACounterOnRowsTheFilterHides()
    {
        var lowering = Persistence()
            .SelectMany(path => Update
                .Matches(File.ReadAllText(path))
                .Select(match => (Path: Relative(path), Match: match)))
            .Where(update => Counters.Any(counter =>
                update.Match.Groups["body"].Value.Contains(counter, StringComparison.Ordinal)))
            .Where(update => Lowers(update.Match.Groups["body"].Value))
            .ToList();

        lowering.Should().NotBeEmpty(
            "a counter that is only ever raised is not a counter, and a walk that finds no " +
            "write lowering one is reading the wrong thing");

        lowering
            .Where(update => !update.Match.Groups["between"].Value
                .Contains("IgnoreQueryFilters", StringComparison.Ordinal))
            .Select(update => update.Path)
            .Should().BeEmpty(
                "the row this has to reach is the author of a post being removed, and the " +
                "posts of deactivated accounts are removed as a matter of course - so under " +
                "the global filter the update matches nothing, reports success, and leaves " +
                "the profile counting a post that is gone");
    }

    /// <summary>A body that subtracts from what it sets rather than adding to it.</summary>
    private static bool Lowers(string body) =>
        Counters.Any(counter => Regex.IsMatch(body, $@"{Regex.Escape(counter)}\s*-\s*\d"));

    private static IEnumerable<string> Persistence() => Directory
        .EnumerateFiles(
            Path.Combine(RepositoryRoot, "src", "DM.Infrastructure.Persistence"),
            "*.cs", SearchOption.AllDirectories)
        .Where(path => !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin" or "Migrations"));

    private static string Relative(string path) => Path.GetRelativePath(RepositoryRoot, path);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
