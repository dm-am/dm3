using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The storage layer reads "now" through the clock abstraction and never off the machine.
/// </summary>
/// <remarks>
/// Everything the layer decides by time — whether an account counts as online, which side of
/// midnight a statistic falls on, how long a bot link code stays valid, what moment a deletion
/// is stamped with — is a rule, and a rule that reads the system clock cannot be stated in a
/// test. Two of these classes used to hold an injected provider and read the machine clock in
/// the same file, so one class carried two different answers to what "now" means.
///
/// Read out of the sources rather than measured, for the same reason SeedDeterminismShould is:
/// the property belongs to the code, while measuring it would need PostgreSQL and MongoDB up.
/// Comments are stripped first, so the paragraph explaining why a call is absent does not
/// count as the call.
/// </remarks>
public class StorageClockShould
{
    /// <summary>
    /// A read of the machine clock, under either spelling.
    /// </summary>
    /// <remarks>
    /// The lookbehind exists to let a property called DateTime through — <c>row.DateTime.Now</c>
    /// is a field of a row and not the framework type. Applied to the bare name alone it also let
    /// the fully qualified form through, because <c>System.DateTimeOffset.UtcNow</c> carries a dot
    /// right where the lookbehind looks: the rule was blind to the one spelling a developer
    /// reaches for when the short one is flagged. The optional <c>System.</c> is therefore matched
    /// explicitly, and the lookbehind now guards the front of the whole reference.
    /// </remarks>
    private static readonly Regex Clock = new(
        @"(?<![\w.])(System\s*\.\s*)?DateTime(Offset)?\s*\.\s*(Now|UtcNow|Today)\b",
        RegexOptions.Compiled);

    /// <summary>
    /// One source of "now" for the whole layer, and it is injected.
    /// </summary>
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
    /// A reader that reads nothing passes everything.
    /// </summary>
    [Fact]
    public void ReadEverySourceFileOfTheLayer()
    {
        var sources = StorageSources();

        sources.Should().HaveCountGreaterThan(50,
            "the layer is the context, the migration and some ninety repository files, and a " +
            "rule that found a handful of them would report green over everything it never " +
            "opened");
        sources.Should().Contain(path => Path.GetFileName(path) == "DmDbContext.cs",
            "the context is where the seed and the model live; if the walk misses it, it is " +
            "walking the wrong directory");
    }

    private static IReadOnlyList<string> StorageSources() => Directory
        .EnumerateFiles(StorageDirectory, "*.cs", SearchOption.AllDirectories)
        .Where(IsAuthored)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToList();

    private static IReadOnlyList<string> Offenders(Regex pattern) => StorageSources()
        .Where(path => pattern.IsMatch(Code(path)))
        .Select(path => Path.GetRelativePath(RepositoryRoot, path))
        .ToList();

    private static string Code(string path) =>
        SourceText.ReadCode(path);

    private static string StorageDirectory =>
        Path.Combine(RepositoryRoot, "src", "DM.Infrastructure.Persistence");

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static bool IsAuthored(string path) =>
        !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin");
}
