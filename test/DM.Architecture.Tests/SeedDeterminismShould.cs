using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The seeded fixture is a function of two values and of nothing else: the
/// instant it is laid out around and the seed its randomness is drawn from.
/// </summary>
/// <remarks>
/// Everything the site is looked at against comes out of the seeder, so a
/// screenshot baseline is worth exactly as much as the fixture behind it is
/// reproducible. Two runs of a seeder that reads a clock or draws from an
/// unseeded generator differ in dates, in who plays where, in what a comment says
/// and at what address a topic lives - and a baseline taken against one of them
/// is red against the other for reasons that have nothing to do with the change
/// under review. That is worse than no baseline: it teaches the reviewer to
/// ignore the colour.
///
/// Read out of the sources rather than measured against a database, because
/// seeding needs PostgreSQL, MongoDB and object storage at once while the
/// property being defended is a property of the code. The measurement is
/// scripts/verify-seed-reproducibility.sh, which runs the seeder twice into the
/// same empty database and diffs the result; this file is what keeps that
/// measurement from silently expiring.
/// </remarks>
public class SeedDeterminismShould
{
    /// <summary>
    /// The single file allowed to read a clock or to build a generator. Every
    /// other file in the tool works from what it resolved.
    /// </summary>
    private const string DeterminismSource = "SeedDeterminism.cs";

    private static readonly Regex Clock = new(
        @"(?<![\w.])DateTime(Offset)?\s*\.\s*(Now|UtcNow|Today)\b", RegexOptions.Compiled);

    private static readonly Regex ClockAbstraction = new(
        @"\bIDateTimeProvider\b", RegexOptions.Compiled);

    private static readonly Regex SharedGenerator = new(
        @"\bRandom\s*\.\s*Shared\b", RegexOptions.Compiled);

    private static readonly Regex Generator = new(
        @"\bnew\s+Random\s*\(", RegexOptions.Compiled);

    private static readonly Regex UnseededGenerator = new(
        @"\bnew\s+Random\s*\(\s*\)", RegexOptions.Compiled);

    private static readonly Regex MintedIdentifier = new(
        @"\bGuid\s*\.\s*NewGuid\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// One resolved epoch, and every seeded date an offset from it.
    /// </summary>
    [Fact]
    public void TakeTheClockOnlyThroughTheSingleResolvedEpoch()
    {
        Offenders(Clock).Should().BeEmpty(
            "a seeded date is an offset from the resolved epoch, and a second clock read puts " +
            "two different \"now\" values into one fixture - the run stops being consistent " +
            "with itself before it even stops being consistent with the next run");

        Offenders(ClockAbstraction, except: DeterminismSource).Should().BeEmpty(
            $"the clock enters the tool in {DeterminismSource} and nowhere else; a second " +
            "entry point is a second default, and the pinned epoch stops covering the seed");
    }

    /// <summary>
    /// One generator, seeded, and the seed itself configurable.
    /// </summary>
    [Fact]
    public void DrawOnlyFromTheSeededGenerator()
    {
        Offenders(SharedGenerator).Should().BeEmpty(
            "Random.Shared is what made the fixture different on every run: an assertion about " +
            "a count or a name held or failed by luck and nothing could be measured twice");

        Offenders(UnseededGenerator).Should().BeEmpty(
            "a generator built without a seed takes one from the machine, which is the same " +
            "defect wearing a local name");

        Offenders(Generator, except: DeterminismSource).Should().BeEmpty(
            $"generators are built in {DeterminismSource}, from the configured seed; one built " +
            "anywhere else is outside the value the run was pinned to");
    }

    /// <summary>
    /// Identifiers are content too: the public id of a page is cut out of one.
    /// </summary>
    [Fact]
    public void MintIdentifiersFromTheSeedRatherThanFromTheOperatingSystem()
    {
        Offenders(MintedIdentifier).Should().BeEmpty(
            "the ten-character public id of a blog notepad and of a game chat is cut straight " +
            "out of a Guid, so a minted one moves the page to a new address on every run and " +
            "no route can be written down");

        var program = Code(Path.Combine(SeederDirectory, "Program.cs"));

        program.Should().Contain("SeededGuidFactory",
            "forbidding Guid.NewGuid in the tool changes nothing while the factory it resolves " +
            "is still the infrastructure one");
        program.Should().Contain("As<IGuidFactory>",
            "the seeded factory has to be registered as the interface the seeder asks for, " +
            "otherwise it is a class nobody constructs");
    }

    /// <summary>
    /// Both values configurable, and both findable by whoever runs the tool.
    /// </summary>
    [Fact]
    public void ResolveBothValuesFromConfiguration()
    {
        var determinism = Code(Path.Combine(SeederDirectory, "Seeding", DeterminismSource));

        determinism.Should().Contain("\"SeedEpochUtc\"",
            "a CI run and a local run have to be able to agree on the instant, and a constant " +
            "in the sources cannot be agreed on from outside");
        determinism.Should().Contain("\"SeedRandomSeed\"",
            "the same for the seed: a hardcoded one is reproducible until the day two runs " +
            "need different fixtures and someone edits it");

        var usage = Code(Path.Combine(SeederDirectory, "Program.cs"));

        usage.Should().Contain("DM_SeedEpochUtc",
            "a knob nobody can find from --help is a knob nobody uses");
        usage.Should().Contain("DM_SeedRandomSeed",
            "the second knob is exactly as invisible as the first one was before it was written down");
    }

    /// <summary>
    /// A reader that reads nothing passes everything.
    /// </summary>
    [Fact]
    public void ReadEverySourceFileOfTheTool()
    {
        var sources = SeederSources();

        sources.Should().HaveCountGreaterThan(10,
            "the seeder is a dozen partial files, and a rule that found one of them would " +
            "report green over the eleven it never opened");
        sources.Should().Contain(path => Path.GetFileName(path) == DeterminismSource,
            "the whole rule set is phrased around that file: if it is gone or renamed, every " +
            "exemption above points at nothing and the rules quietly loosen");
    }

    /// <summary>The seeder sources the rules above are read out of.</summary>
    private static IReadOnlyList<string> SeederSources() => Directory
        .EnumerateFiles(SeederDirectory, "*.cs", SearchOption.AllDirectories)
        .Where(IsAuthored)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToList();

    /// <summary>Files matching the pattern, named relative to the repository root.</summary>
    private static IReadOnlyList<string> Offenders(Regex pattern, string? except = null) => SeederSources()
        .Where(path => except == null ||
                       !string.Equals(Path.GetFileName(path), except, StringComparison.Ordinal))
        .Where(path => pattern.IsMatch(Code(path)))
        .Select(path => Path.GetRelativePath(RepositoryRoot, path))
        .ToList();

    /// <summary>The file's text with its comments removed.</summary>
    private static string Code(string path) =>
        SourceText.ReadCode(path);

    private static string SeederDirectory => Path.Combine(RepositoryRoot, "src", "DM.Tools.Seeder");

    /// <summary>
    /// Walks up from the test binary to the repository root. The sources are not
    /// copied to the output directory, and copying them would let this assert
    /// against a stale snapshot.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !(Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                     Directory.Exists(Path.Combine(directory.FullName, "test"))))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }

    private static bool IsAuthored(string path) =>
        !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin");
}
