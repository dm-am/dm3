using System;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// The gate runs the --check of scripts/docs/build-status.mjs. Without the client's packages it exits 2,
/// which is a red run, not a skipped one.
/// </remarks>
public class StatusDocumentShould
{
    private const string Document = "docs/STATUS.md";

    private const string Script = "scripts/docs/build-status.mjs";

    private const int NoCompiler = 2;

    private const int ShownDiffLines = 20;

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    [Fact]
    public void MatchWhatItsScriptCounts()
    {
        var (exitCode, output, error) = RepositoryFiles.Run("node", $"{Script} --check", timeout: Timeout);

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
}
