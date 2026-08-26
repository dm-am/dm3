using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DM.Architecture.Tests;

/// <summary>
/// The files of the checkout, walked the way the rules of this tier walk them.
/// </summary>
/// <remarks>
/// Which directories a walk steps into is a decision of the rule doing the
/// walking, not of the walk, so the skipped set stays an argument: one rule reads
/// the C# of the whole solution and has to step over both toolchains' output,
/// another reads one project and only ever meets bin and obj. Merging those sets
/// would quietly widen or narrow what a gate looks at.
///
/// What is shared is the walk itself, which was written out once per rule and is
/// the same enumeration every time.
/// </remarks>
internal static class RepositoryFiles
{
    /// <summary>
    /// Every C# file the product is written in, build output left out.
    /// </summary>
    public static IEnumerable<string> ProductionSources() => Directory
        .EnumerateFiles(Path.Combine(DM.Testing.RepositoryLayout.Root, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(path => !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin"));

    /// <summary>
    /// The C# files under a directory, without descending into the directories
    /// named.
    /// </summary>
    /// <param name="directory">Directory to walk</param>
    /// <param name="skipped">Directory names not to step into</param>
    /// <returns>Paths of the C# files found</returns>
    public static IEnumerable<string> CsharpFiles(string directory, IReadOnlyCollection<string> skipped)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
        {
            yield return file;
        }

        foreach (var nested in Directory.EnumerateDirectories(directory))
        {
            if (skipped.Contains(Path.GetFileName(nested)))
            {
                continue;
            }

            foreach (var file in CsharpFiles(nested, skipped))
            {
                yield return file;
            }
        }
    }
}
