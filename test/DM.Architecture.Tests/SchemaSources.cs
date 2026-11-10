using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;

namespace DM.Architecture.Tests;

// Regenerating the migration mints a new timestamp, so it is found by pattern and asserted to be
// one.
internal static class SchemaSources
{
    private static string MigrationsDirectory => Path.Combine(
        DM.Testing.RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence", "Migrations");

    internal static string Migration => File.ReadAllText(Single("*_InitialCreate.cs"));

    // Both, generated from the same model, drift apart silently.
    internal static IEnumerable<(string Name, string Text)> Snapshots =>
        new[]
        {
            Single("*_InitialCreate.Designer.cs"),
            Path.Combine(MigrationsDirectory, "DmDbContextModelSnapshot.cs"),
        }
        .Select(path => (Path.GetFileName(path), File.ReadAllText(path)));

    private static string PersistenceDirectory => Path.Combine(
        DM.Testing.RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence");

    /// <remarks>
    /// Found by what they are, so cutting the context per module keeps them found. Narrow: two rules let
    /// these files spell what no other file may.
    /// </remarks>
    internal static IReadOnlyList<string> ModelFiles
    {
        get
        {
            var files = Directory
                .EnumerateFiles(PersistenceDirectory, "*.cs", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .Where(path => !Segments(path).Any(segment => segment is "bin" or "obj" or "Migrations"))
                .Where(path => IsModelFile(Path.GetRelativePath(PersistenceDirectory, path), SourceText.ReadCode(path)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            files.Should().Contain(path => SourceText.ReadCode(path).Contains(ModelEntry, StringComparison.Ordinal),
                "the model is declared somewhere in the persistence layer, and a walk that finds " +
                "no declaration hands every rule reading it an empty text to pass over");

            return files;
        }
    }

    /// <summary>The authored model, every file of it, as one text.</summary>
    internal static string ModelText => string.Join("\n", ModelFiles.Select(File.ReadAllText));

    internal const string ModelFolder = "ModelConfiguration";

    private const string ModelEntry = "override void OnModelCreating(";

    internal static bool IsModelFile(string relativePath, string code) =>
        Segments(relativePath).Contains(ModelFolder, StringComparer.Ordinal) ||
        code.Contains(ModelEntry, StringComparison.Ordinal);

    private static string[] Segments(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    internal const string ModelStart = "#pragma warning disable 612, 618";

    internal static string Model(string text) => text[text.IndexOf(ModelStart, StringComparison.Ordinal)..];

    // A search pattern ending in a three-character extension matches longer ones, ".cs" is not
    // three.
    private static string Single(string pattern)
    {
        var matches = Directory.GetFiles(MigrationsDirectory, pattern);

        matches.Should().ContainSingle(
            $"the schema is built from one migration, and {pattern} matched {matches.Length} files");

        return matches[0];
    }
}
