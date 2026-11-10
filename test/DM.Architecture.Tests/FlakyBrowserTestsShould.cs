using System;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// A test passed on a retry makes the job green. The reporter writes a file and a workflow step reads
/// it, so the path is read from both files and compared.
/// </remarks>
public class FlakyBrowserTestsShould
{
    private const string Config = "src/DM.Web.Client/playwright.config.ts";

    /// <summary>The output file of the json reporter, as the config declares it.</summary>
    private static readonly Regex JsonReporter = new(
        @"\[\s*""json""\s*,\s*\{\s*outputFile:\s*""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void BeCountedOnTheRunPageRatherThanOnlyInsideAnArtifact()
    {
        var config = RepositoryFiles.Read(Config);

        config.Should().Contain("retries: process.env.CI",
            "this rule exists because a retry can turn a red test green, and a config " +
            "that no longer retries would leave it asserting about nothing");

        var reporter = JsonReporter.Match(config);
        reporter.Success.Should().BeTrue(
            $"{Config} must declare a json reporter with an output file: the HTML report " +
            "is not readable by a step");

        var outputFile = reporter.Groups[1].Value;
        var e2e = string.Join('\n', WorkflowJobs.Body("dotnet.yml", "e2e"));

        e2e.Should().Contain(outputFile,
            $"the step that summarises the run has to read the file the reporter writes, " +
            $"and the reporter writes {outputFile}");
        e2e.Should().Contain("GITHUB_STEP_SUMMARY",
            "printing into the log of a step nobody expands is the artifact problem again");
        e2e.Should().Contain("flaky",
            "the summary is about the tests that needed a retry, not about the run as a whole");
    }

    private const string Shard = "${{ matrix.shard }}";

    private static string[] StepOf(string job, string name)
    {
        var lines = job.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !line.TrimStart().StartsWith('#'))
            .ToArray();

        var start = Array.IndexOf(lines, $"    - name: {name}");
        start.Should().BeGreaterThan(-1, $"the e2e job must declare the step \"{name}\"");

        var end = start + 1;
        while (end < lines.Length && !lines[end].StartsWith("    - ", StringComparison.Ordinal))
        {
            end++;
        }

        return lines[start..end];
    }

    // An artifact name is unique within a run, so legs uploading under one name fail.
    [Fact]
    public void ReportEveryShareUnderItsOwnName()
    {
        var e2e = string.Join('\n', WorkflowJobs.Body("dotnet.yml", "e2e"));

        var artifact = StepOf(e2e, "Upload Playwright report")
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("name:", StringComparison.Ordinal))
            .ToArray();

        artifact.Should().ContainSingle("the step names the artifact it uploads, under with:");
        artifact[0].Should().Contain(Shard,
            "the legs upload into one run, and a second artifact under a name already taken " +
            "fails its leg and loses the report of that share");

        string.Join('\n', StepOf(e2e, "Flaky tests of this run")).Should().Contain(Shard,
            "the summary names the share it counted the flakes of");
    }
}
