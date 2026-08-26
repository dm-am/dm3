using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A browser test that only passed on a retry is named on the run page.
/// </summary>
/// <remarks>
/// The end-to-end tier runs with retries: 2 in CI, which is the right setting for
/// a suite driving a real browser and the wrong one to leave unreported: a test
/// that failed twice and passed on the third attempt makes the job green, and the
/// only evidence used to live inside an HTML artifact somebody has to download and
/// open. Nothing counted them, so nothing could say a spec had been flaky for a
/// month — and a spec nobody can prove is flaky gets deleted for being "always red
/// anyway".
///
/// The coupling is the part a reader cannot see: the reporter writes a file and a
/// step of the workflow reads it, and a rename on either side leaves a step that
/// prints "no report" on every run and a gate that is green about nothing. So the
/// path is read from both files and compared, rather than written down here.
/// </remarks>
public class FlakyBrowserTestsShould
{
    private const string Config = "src/DM.Web.Client/playwright.config.ts";
    private const string Workflow = ".github/workflows/dotnet.yml";

    /// <summary>The output file of the json reporter, as the config declares it.</summary>
    private static readonly Regex JsonReporter = new(
        @"\[\s*""json""\s*,\s*\{\s*outputFile:\s*""([^""]+)""", RegexOptions.Compiled);

    /// <summary>A job: below jobs: it is the only key written at two spaces.</summary>
    private static readonly Regex JobName = new(@"^  ([a-z][a-z0-9-]*):\s*$", RegexOptions.Compiled);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string Read(string relative) => File.ReadAllText(
        Path.Combine(RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>The lines of one job, from its key to the next job's.</summary>
    private static string JobBody(string workflow, string job)
    {
        var lines = workflow.Split('\n');
        var start = Array.FindIndex(
            lines, line => line.StartsWith($"  {job}:", StringComparison.Ordinal));
        start.Should().BeGreaterThan(-1, $"the workflow must declare the {job} job");

        var end = start + 1;
        while (end < lines.Length && !JobName.IsMatch(lines[end].TrimEnd('\r')))
        {
            end++;
        }

        return string.Join('\n', lines[start..end]);
    }

    [Fact]
    public void BeCountedOnTheRunPageRatherThanOnlyInsideAnArtifact()
    {
        var config = Read(Config);

        config.Should().Contain("retries: process.env.CI",
            "this rule exists because a retry can turn a red test green, and a config " +
            "that no longer retries would leave it asserting about nothing");

        var reporter = JsonReporter.Match(config);
        reporter.Success.Should().BeTrue(
            $"{Config} must declare a json reporter with an output file: the HTML report " +
            "is not readable by a step");

        var outputFile = reporter.Groups[1].Value;
        var e2e = JobBody(Read(Workflow), "e2e");

        e2e.Should().Contain(outputFile,
            $"the step that summarises the run has to read the file the reporter writes, " +
            $"and the reporter writes {outputFile}");
        e2e.Should().Contain("GITHUB_STEP_SUMMARY",
            "printing into the log of a step nobody expands is the artifact problem again");
        e2e.Should().Contain("flaky",
            "the summary is about the tests that needed a retry, not about the run as a whole");
    }
}
