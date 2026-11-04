using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A character lost in transcoding does not get committed.
/// </summary>
/// <remarks>
/// U+FFFD is what a decoder writes for bytes it could not read. Both markers are spelled by code point;
/// src and test only, the audit documents quote the damage.
/// </remarks>
public class SourceEncodingShould
{
    /// <summary>U+FFFD REPLACEMENT CHARACTER.</summary>
    private const char Replacement = '\uFFFD';

    private static readonly string[] Extensions = [".cs", ".ts", ".vue", ".json", ".sh", ".ps1"];

    [Fact]
    public void FindTheFilesItReads() =>
        AuthoredSources().Should().NotBeEmpty("a walk that matches nothing passes");

    [Fact]
    public void CarryNoReplacementCharacter() =>
        AuthoredSources()
            .Where(path => File.ReadAllText(path).Contains(Replacement))
            .Select(RepositoryFiles.Relative)
            .Should().BeEmpty(
                "U+FFFD is the trace of a decoder that did not read the file as UTF-8, " +
                "and the next thing that tool touches may be a string a reader sees");

    private static IReadOnlyList<string> AuthoredSources() => new[] { "src", "test" }
        .Select(tree => Path.Combine(RepositoryLayout.Root, tree))
        .SelectMany(tree => Directory.EnumerateFiles(tree, "*.*", SearchOption.AllDirectories))
        .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        .Where(IsAuthored)
        .ToArray();

    private static bool IsAuthored(string path) => !path
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin" or "node_modules" or "dist" or "coverage");
}
