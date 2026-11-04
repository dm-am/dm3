using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using AwesomeAssertions;
using DM.Testing;

namespace DM.Architecture.Tests;

/// <summary>
/// The files of the checkout, walked the way the rules of this tier walk them.
/// </summary>
/// <remarks>The skipped set of a walk stays an argument: merging the sets would widen or narrow what a gate reads.</remarks>
internal static class RepositoryFiles
{
    /// <summary>
    /// Every C# file the product is written in, build output left out.
    /// </summary>
    public static IEnumerable<string> ProductionSources() => Directory
        .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(IsAuthored);

    /// <summary>Whether a path lies outside the output of both toolchains.</summary>
    public static bool IsAuthored(string path) => !path
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin" or "node_modules");

    /// <summary>The text of a file of the checkout, by its path from the root.</summary>
    /// <param name="parts">The path from the root, in parts or with forward slashes</param>
    /// <returns>The text of the file; a missing file fails the rule that reads it</returns>
    public static string Read(params string[] parts)
    {
        var relative = string.Join('/', parts);
        var path = Path.Combine(RepositoryLayout.Root, relative.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"{relative} must exist at {path}");
        return File.ReadAllText(path);
    }

    /// <summary>A path as written from the root of the checkout.</summary>
    public static string Relative(string path) => Path.GetRelativePath(RepositoryLayout.Root, path);

    /// <summary>
    /// The files under a directory, without descending into the directories named.
    /// </summary>
    /// <param name="directory">Directory to walk</param>
    /// <param name="pattern">Search pattern of the file names</param>
    /// <param name="skipped">Directory names not to step into</param>
    /// <returns>Paths of the files found</returns>
    public static IEnumerable<string> Files(string directory, string pattern, IReadOnlyCollection<string> skipped)
    {
        foreach (var file in Directory.EnumerateFiles(directory, pattern))
        {
            yield return file;
        }

        foreach (var nested in Directory.EnumerateDirectories(directory))
        {
            if (skipped.Contains(Path.GetFileName(nested)))
            {
                continue;
            }

            foreach (var file in Files(nested, pattern, skipped))
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
        var (exitCode, output, error) = Run("git", "ls-files -z --cached --others --exclude-standard");
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"git ls-files failed: {error}");
        }

        // Separated by NUL so that a name in Cyrillic arrives as written, not quoted.
        return output
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Select(relative => Path.GetFullPath(Path.Combine(RepositoryLayout.Root, relative)))
            .Where(File.Exists)
            .ToList();
    }

    /// <summary>Runs a program at the root of the checkout and returns its exit code and both streams.</summary>
    /// <param name="program">Program to start</param>
    /// <param name="arguments">Its command line</param>
    /// <param name="input">Text written to its standard input, or null for none</param>
    /// <param name="timeout">How long to wait before the process tree is killed, or null to wait for it</param>
    /// <param name="environment">Variables taken out of the inherited environment</param>
    /// <returns>Exit code, standard output and standard error</returns>
    public static (int ExitCode, string Output, string Error) Run(
        string program,
        string arguments,
        string? input = null,
        TimeSpan? timeout = null,
        params string[] environment)
    {
        var start = new ProcessStartInfo(program, arguments)
        {
            WorkingDirectory = RepositoryLayout.Root,
            RedirectStandardInput = input != null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        if (input != null)
        {
            // No preamble: a BOM in front of the first line is a part of it.
            start.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        foreach (var variable in environment)
        {
            start.Environment.Remove(variable);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"{program} could not be started at the repository root");

        if (input != null)
        {
            process.StandardInput.Write(input);
            process.StandardInput.Close();
        }

        // Both pipes drained before the wait: a full pipe blocks the child nobody reads.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout ?? Timeout.InfiniteTimeSpan))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{program} {arguments} did not finish in {timeout}");
        }

        return (process.ExitCode, output.Result, error.Result);
    }
}
