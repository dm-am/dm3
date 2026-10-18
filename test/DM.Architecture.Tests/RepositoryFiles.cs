using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

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

    /// <summary>
    /// The files a commit of the checkout would carry, as full paths: the ones git
    /// tracks and the new ones it would add, and nothing the ignore list names.
    /// </summary>
    /// <remarks>
    /// Asked of git rather than walked, for the rules that read the whole tree. A
    /// walk reads whatever lies on this disk, and that includes build output, the
    /// dependencies of the client and the full copies of the tree other sessions
    /// keep under .claude/worktrees; a list of folders to step over falls behind
    /// the first new one, while the ignore list already is that list and is kept
    /// for its own reasons. A new file is read because the next commit carries
    /// it; a tracked file already deleted on disk is not.
    /// </remarks>
    public static IReadOnlyList<string> CommittableFiles() => Committable.Value;

    private static readonly Lazy<IReadOnlyList<string>> Committable = new(ListCommittable);

    private static IReadOnlyList<string> ListCommittable()
    {
        var root = DM.Testing.RepositoryLayout.Root;
        var start = new ProcessStartInfo("git", "ls-files -z --cached --others --exclude-standard")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("git could not be started inside the checkout");

        // Both pipes drained before the wait: a full pipe blocks the child nobody reads.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git ls-files failed: {error.Result}");
        }

        // Separated by NUL so that a name in Cyrillic arrives as written, not quoted.
        return output.Result
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Select(relative => Path.GetFullPath(Path.Combine(root, relative)))
            .Where(File.Exists)
            .ToList();
    }
}
