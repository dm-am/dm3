using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;

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
