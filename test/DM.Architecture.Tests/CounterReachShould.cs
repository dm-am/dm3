using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A counter that goes up on a live row comes back down on a row that may not be.
/// </summary>
/// <remarks>
/// The soft-delete filter is global, so a set-based update that lowers a counter misses removed
/// rows and reports success. Asserted on the text: ExecuteUpdate has no in-memory provider.
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
                .Select(match => (Path: RepositoryFiles.Relative(path), Match: match)))
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
            Path.Combine(RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence"),
            "*.cs", SearchOption.AllDirectories)
        .Where(path => !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin" or "Migrations"));
}
