using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// A screenshot baseline is worth as much as the fixture behind it is reproducible. Read out of the
/// sources; scripts/verify-seed-reproducibility.sh measures it against a database.
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

    // Identifiers are content too: the public id of a page is cut out of one.
    [Fact]
    public void MintIdentifiersFromTheSeedRatherThanFromTheOperatingSystem()
    {
        Offenders(MintedIdentifier).Should().BeEmpty(
            "the ten-character public id of a blog notepad and of a game chat is cut straight " +
            "out of a Guid, so a minted one moves the page to a new address on every run and " +
            "no route can be written down");

        var program = SourceText.ReadCode(Path.Combine(SeederDirectory, "Program.cs"));

        program.Should().Contain("SeededGuidFactory",
            "forbidding Guid.NewGuid in the tool changes nothing while the factory it resolves " +
            "is still the infrastructure one");
        program.Should().Contain("AddSingleton<IGuidFactory, SeededGuidFactory>",
            "the seeded factory has to be registered as the interface the seeder asks for - " +
            "and after every module call, because MS.DI hands the resolution to the last " +
            "registration - otherwise it is a class nobody constructs");
    }

    [Fact]
    public void ResolveBothValuesFromConfiguration()
    {
        var determinism = SourceText.ReadCode(Path.Combine(SeederDirectory, "Seeding", DeterminismSource));

        determinism.Should().Contain("\"SeedEpochUtc\"",
            "a CI run and a local run have to be able to agree on the instant, and a constant " +
            "in the sources cannot be agreed on from outside");
        determinism.Should().Contain("\"SeedRandomSeed\"",
            "the same for the seed: a hardcoded one is reproducible until the day two runs " +
            "need different fixtures and someone edits it");

        var usage = SourceText.ReadCode(Path.Combine(SeederDirectory, "Program.cs"));

        usage.Should().Contain("DM_SeedEpochUtc",
            "a knob nobody can find from --help is a knob nobody uses");
        usage.Should().Contain("DM_SeedRandomSeed",
            "the second knob is exactly as invisible as the first one was before it was written down");
    }

    // A reader that reads nothing passes everything.
    [Fact]
    public void ReadEverySourceFileOfTheTool()
    {
        var sources = SeederSources();

        sources.Should().NotBeEmpty(
            "a rule that found no file of the seeder would report green over everything " +
            "it never opened");
        sources.Should().Contain(path => Path.GetFileName(path) == DeterminismSource,
            "the whole rule set is phrased around that file: if it is gone or renamed, every " +
            "exemption above points at nothing and the rules quietly loosen");
    }

    private static IReadOnlyList<string> SeederSources() => Directory
        .EnumerateFiles(SeederDirectory, "*.cs", SearchOption.AllDirectories)
        .Where(RepositoryFiles.IsAuthored)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToList();

    private static IReadOnlyList<string> Offenders(Regex pattern, string? except = null) => SeederSources()
        .Where(path => except == null ||
                       !string.Equals(Path.GetFileName(path), except, StringComparison.Ordinal))
        .Where(path => pattern.IsMatch(SourceText.ReadCode(path)))
        .Select(RepositoryFiles.Relative)
        .ToList();

    private static string SeederDirectory => Path.Combine(RepositoryLayout.Root, "src", "DM.Tools.Seeder");
}
