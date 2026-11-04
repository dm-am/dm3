using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;

namespace DM.Architecture.Tests;

/// <summary>
/// The files the schema is built from, found rather than named.
/// </summary>
/// <remarks>Regenerating the migration mints a new timestamp, so it is found by pattern and asserted to be one.</remarks>
internal static class SchemaSources
{
    private static string MigrationsDirectory => Path.Combine(
        DM.Testing.RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence", "Migrations");

    /// <summary>The migration the whole schema is created by.</summary>
    internal static string Migration => File.ReadAllText(Single("*_InitialCreate.cs"));

    /// <summary>
    /// The two files that describe the model to the tooling, with their names.
    /// </summary>
    /// <remarks>Both, generated from the same model, drift apart silently.</remarks>
    internal static IEnumerable<(string Name, string Text)> Snapshots =>
        new[]
        {
            Single("*_InitialCreate.Designer.cs"),
            Path.Combine(MigrationsDirectory, "DmDbContextModelSnapshot.cs"),
        }
        .Select(path => (Path.GetFileName(path), File.ReadAllText(path)));

    private static string PersistenceDirectory => Path.Combine(
        DM.Testing.RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence");

    /// <summary>
    /// The authored files the model is declared in: the one that overrides
    /// OnModelCreating, and every file of the folder the model is cut into by
    /// module.
    /// </summary>
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

    /// <summary>The folder the model is cut into, a file per module.</summary>
    internal const string ModelFolder = "ModelConfiguration";

    private const string ModelEntry = "override void OnModelCreating(";

    /// <summary>
    /// Whether a file of the persistence project, by its path inside the project
    /// and its code without comments, is part of the model.
    /// </summary>
    internal static bool IsModelFile(string relativePath, string code) =>
        Segments(relativePath).Contains(ModelFolder, StringComparer.Ordinal) ||
        code.Contains(ModelEntry, StringComparison.Ordinal);

    private static string[] Segments(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>Where the generated description of the model starts in both files.</summary>
    internal const string ModelStart = "#pragma warning disable 612, 618";

    /// <summary>The generated body of a snapshot, without the header naming it.</summary>
    internal static string Model(string text) => text[text.IndexOf(ModelStart, StringComparison.Ordinal)..];

    /// <summary>
    /// The one file matching a pattern.
    /// </summary>
    /// <remarks>A search pattern ending in a three-character extension matches longer ones, ".cs" is not three.</remarks>
    private static string Single(string pattern)
    {
        var matches = Directory.GetFiles(MigrationsDirectory, pattern);

        matches.Should().ContainSingle(
            $"the schema is built from one migration, and {pattern} matched {matches.Length} files");

        return matches[0];
    }
}
