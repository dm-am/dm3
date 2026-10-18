using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// docs/STATUS.md says what the tree says: it is the output of its script, run on
/// this checkout, to the byte.
/// </summary>
/// <remarks>
/// The state of the site was kept by hand, as marks in a table, and a mark is true
/// on the day it is set and then drifts with every change nobody remembers to
/// mirror. The status is counted instead: routes, frames of the stand, endpoints
/// without a caller, pages nothing links to, and which documents a test holds.
/// What the script reads and how is written at the top of
/// scripts/docs/build-status.mjs; this gate only runs its --check, which exits 1
/// with a diff when the committed file differs from the count.
///
/// The script parses with the TypeScript compiler of the client, so the client's
/// packages have to be installed; without them it exits 2, and that is a red run
/// rather than a skipped one, because a gate that steps aside where it cannot
/// look checks nothing there.
/// </remarks>
public class StatusDocumentShould
{
    private const string Document = "docs/STATUS.md";

    private const string Script = "scripts/docs/build-status.mjs";

    /// <summary>The exit code of the script when the client's packages are not installed.</summary>
    private const int NoCompiler = 2;

    private const int ShownDiffLines = 20;

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    [Fact]
    public void MatchWhatItsScriptCounts()
    {
        var (exitCode, output, error) = Node($"{Script} --check");

        exitCode.Should().NotBe(NoCompiler,
            $"{Script} разбирает код компилятором клиента: выполни npm ci в src/DM.Web.Client{Environment.NewLine}" +
            error);

        var diff = string.Join(
            Environment.NewLine,
            output.Split('\n').Take(ShownDiffLines).Select(line => line.TrimEnd('\r')));

        exitCode.Should().Be(0,
            $"{Document} устарел: запусти node {Script} и закоммить{Environment.NewLine}" +
            $"{diff}{Environment.NewLine}{error}");
    }

    /// <summary>Runs node at the repository root and returns its exit code and both streams.</summary>
    private static (int ExitCode, string Output, string Error) Node(string arguments)
    {
        var start = new ProcessStartInfo("node", arguments)
        {
            WorkingDirectory = DM.Testing.RepositoryLayout.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("node could not be started at the repository root");

        // Both pipes drained before the wait: a full pipe blocks the child nobody reads.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(Timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"node {arguments} did not finish in {Timeout}");
        }

        return (process.ExitCode, output.Result, error.Result);
    }
}
