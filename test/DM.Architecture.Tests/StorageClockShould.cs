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
/// The storage layer reads "now" through the clock abstraction and never off the machine.
/// </summary>
/// <remarks>
/// A rule that reads the system clock cannot be stated in a test. Read out of the sources with comments
/// stripped, as SeedDeterminismShould is.
/// </remarks>
public class StorageClockShould
{
    /// <remarks>
    /// The lookbehind lets a property called DateTime through (<c>row.DateTime.Now</c>); the optional
    /// <c>System.</c> is matched explicitly so the qualified form is caught.
    /// </remarks>
    private static readonly Regex Clock = new(
        @"(?<![\w.])(System\s*\.\s*)?DateTime(Offset)?\s*\.\s*(Now|UtcNow|Today)\b",
        RegexOptions.Compiled);

    [Fact]
    public void TakeTheClockOnlyThroughTheInjectedProvider()
    {
        Offenders(Clock).Should().BeEmpty(
            "a repository that reads the machine clock cannot be put in front of a fixed " +
            "instant, so the rules it decides by time - the online window, the day boundary, " +
            "the lifetime of a link code, the moment a row is marked deleted - have no test " +
            "that can state them; and a class that does it while already holding " +
            "IDateTimeProvider carries two different answers to the same question");
    }

    /// <summary>
    /// A generated identifier, taken off the framework instead of the factory.
    /// </summary>
    private static readonly Regex Identifier = new(
        @"(?<![\w.])(System\s*\.\s*)?Guid\s*\.\s*NewGuid\s*\(",
        RegexOptions.Compiled);

    // The seeding tool replaces IGuidFactory with a deterministic one, so a direct call breaks
    // reproduction.
    [Fact]
    public void TakeNewIdentifiersOnlyThroughTheInjectedFactory()
    {
        Offenders(Identifier).Should().BeEmpty(
            "the seeder swaps the factory for a deterministic one, and a repository " +
            "that bypasses it hands out an identifier no fixture can predict");
    }

    // A reader that reads nothing passes everything.
    [Fact]
    public void ReadEverySourceFileOfTheLayer()
    {
        var sources = StorageSources();

        sources.Should().NotBeEmpty(
            "a rule that found none of the files of the layer would report green over " +
            "everything it never opened");
        sources.Should().Contain(path => Path.GetFileName(path) == "DmDbContext.cs",
            "the context is where the seed and the model live; if the walk misses it, it is " +
            "walking the wrong directory");
    }

    private static IReadOnlyList<string> StorageSources() => Directory
        .EnumerateFiles(StorageDirectory, "*.cs", SearchOption.AllDirectories)
        .Where(RepositoryFiles.IsAuthored)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToList();

    private static IReadOnlyList<string> Offenders(Regex pattern) => StorageSources()
        .Where(path => pattern.IsMatch(SourceText.ReadCode(path)))
        .Select(RepositoryFiles.Relative)
        .ToList();

    private static string StorageDirectory =>
        Path.Combine(RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence");
}
