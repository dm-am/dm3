using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A character lost in transcoding does not get committed.
/// </summary>
/// <remarks>
/// U+FFFD is what a decoder writes when it read bytes it could not interpret, so a
/// file holding one has been through a tool that does not treat the tree as UTF-8.
/// Three comments in the composition root carried it in place of a dash, and two log
/// lines of one consumer carried a pair of question marks where its twin has emoji.
/// Neither breaks anything, which is the problem: the same round trip lands on a
/// Russian interface string or a seeded description next time, and there the reader
/// is the one who finds it.
///
/// Both markers are spelled by code point so this file stays clean of what it
/// forbids, and the walk covers src and test only - the audit documents quote the
/// damage on purpose.
/// </remarks>
public class SourceEncodingShould
{
    /// <summary>U+FFFD REPLACEMENT CHARACTER.</summary>
    private const char Replacement = '\uFFFD';

    /// <summary>
    /// What a non-ASCII character becomes when a console cannot render it and the
    /// result is written back. Not a sequence anybody types.
    /// </summary>
    // Spelled by code point so this file is not itself a hit for the rule it holds.
    private const string LostGlyphs = "[\u003F\u003F]";

    private static readonly string[] Extensions = [".cs", ".ts", ".vue", ".json", ".sh", ".ps1"];

    [Fact]
    public void FindTheFilesItReads() =>
        AuthoredSources().Should().HaveCountGreaterThan(1000,
            "a walk that matches nothing passes, and this tree holds thousands of authored files");

    [Fact]
    public void CarryNoReplacementCharacter() =>
        AuthoredSources()
            .Where(path => File.ReadAllText(path).Contains(Replacement))
            .Select(Relative)
            .Should().BeEmpty(
                "U+FFFD is the trace of a decoder that did not read the file as UTF-8, " +
                "and the next thing that tool touches may be a string a reader sees");

    [Fact]
    public void CarryNoGlyphLostToAConsole() =>
        AuthoredSources()
            .Where(path => File.ReadAllText(path).Contains(LostGlyphs, StringComparison.Ordinal))
            .Select(Relative)
            .Should().BeEmpty(
                "the sequence is what an emoji becomes when it survives neither the " +
                "console nor the write back, and it is never typed on purpose");

    private static IReadOnlyList<string> AuthoredSources() => new[] { "src", "test" }
        .Select(tree => Path.Combine(RepositoryRoot, tree))
        .SelectMany(tree => Directory.EnumerateFiles(tree, "*.*", SearchOption.AllDirectories))
        .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        .Where(IsAuthored)
        .ToArray();

    private static bool IsAuthored(string path) => !path
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin" or "node_modules" or "dist" or "coverage");

    private static string Relative(string path) => Path.GetRelativePath(RepositoryRoot, path);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
