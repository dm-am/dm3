using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;

namespace DM.Architecture.Tests;

/// <summary>
/// The files the schema is built from, found rather than named.
/// </summary>
/// <remarks>
/// The migration carries a timestamp in its file name, and the documented way to
/// change the schema is to regenerate it — which mints a new timestamp. Spelled
/// out as a literal, the name made every rule that reads the schema fail with a
/// missing file the moment somebody followed the documented procedure, and fail
/// in a way that says nothing about what it was checking.
///
/// Found by pattern, and asserted to be one: two migrations matching would mean
/// the single-migration rule had already been broken, and the reader of a green
/// run deserves to know which of the two was checked.
/// </remarks>
internal static class SchemaSources
{
    private static string MigrationsDirectory => Path.Combine(
        DM.Testing.RepositoryLayout.Root, "src", "DM.Infrastructure.Persistence", "Migrations");

    /// <summary>The migration the whole schema is created by.</summary>
    internal static string Migration => File.ReadAllText(Single("*_InitialCreate.cs"));

    /// <summary>
    /// The two files that describe the model to the tooling, with their names.
    /// </summary>
    /// <remarks>
    /// Both, because they are generated from the same model and drift apart
    /// silently: the snapshot decides what the next migration would contain, and
    /// the designer decides what this one is understood to have done.
    /// </remarks>
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
    /// Found by what they are rather than by the name of the context: a rule that
    /// reads the model by one file name fails the day the context is cut into a
    /// file per module, and the failure names a missing declaration instead of
    /// the rule it holds.
    ///
    /// Narrow on purpose, because two rules let these files spell what no other
    /// file may (the newbie threshold and the ticket conversation). A file
    /// elsewhere that merely takes a model builder is not the model and does not
    /// get that licence, and neither do the catalogues of initial data, which
    /// hand rows to the model rather than declare it. The generated migration
    /// files are read through <see cref="Migration"/> and <see cref="Snapshots"/>.
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
    /// <remarks>
    /// The pattern for the migration does not catch its designer file: a search
    /// pattern ending in a three-character extension matches longer ones too, and
    /// ".cs" is not three characters.
    /// </remarks>
    private static string Single(string pattern)
    {
        var matches = Directory.GetFiles(MigrationsDirectory, pattern);

        matches.Should().ContainSingle(
            $"the schema is built from one migration, and {pattern} matched {matches.Length} files");

        return matches[0];
    }
}
