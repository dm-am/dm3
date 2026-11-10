using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

// "Every other job in the file", not a list: a new job is in needs or named here as an exception.
public class PublishGraphShould
{
    private const string Workflow = ".github/workflows/dotnet.yml";

    private const string Promotion = "promote-images";

    private static readonly Regex NeedsList = new(@"^    needs:\s*\[([^\]]*)\]", RegexOptions.Compiled);

    private static IReadOnlyDictionary<string, string[]> Jobs() => WorkflowJobs.Read("dotnet.yml")
        .ToDictionary(
            job => job.Name,
            job => job.Lines
                .Select(line => NeedsList.Match(line))
                .LastOrDefault(needs => needs.Success)?.Groups[1].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
            StringComparer.Ordinal);

    [Fact]
    public void WaitForEveryCheckTheWorkflowRuns()
    {
        var jobs = Jobs();
        jobs.Should().NotBeEmpty("a reader that finds no jobs asserts nothing");

        var publishers = jobs.Keys.Where(name => name.StartsWith("publish", StringComparison.OrdinalIgnoreCase)).ToArray();
        publishers.Should().NotBeEmpty("the rule is about the jobs that push images");

        foreach (var publisher in publishers)
        {
            var expected = jobs.Keys
                .Where(name => name != publisher)
                .Where(name => !name.StartsWith("publish", StringComparison.OrdinalIgnoreCase))
                // The promotion runs after the publishers: waiting for it would be a cycle.
                .Where(name => name != Promotion)
                .OrderBy(name => name, StringComparer.Ordinal);

            jobs[publisher].OrderBy(name => name, StringComparer.Ordinal)
                .Should().Equal(expected,
                    $"{publisher} pushes an image, and a check it does not wait for is a " +
                    "check that cannot stop the release");
        }
    }

    private const string EndToEnd = "e2e";

    private static readonly Regex ShardLegs = new(@"^shard:\s*\[([^\]]*)\]$", RegexOptions.Compiled);

    private static readonly Regex ShardFlag = new(
        @"--shard=\$\{\{\s*matrix\.shard\s*\}\}/(\$\{\{\s*strategy\.job-total\s*\}\}|\d+)\s*$",
        RegexOptions.Compiled);

    private static List<string> JobLines(string job) =>
        WorkflowJobs.Body("dotnet.yml", job).Where(WorkflowJobs.IsCode).ToList();

    private static int IndentOf(string line) => line.Length - line.TrimStart().Length;

    /// <remarks>
    /// A share no leg runs gates nothing. The count is read from strategy.job-total, so the shard is the
    /// only dimension of this matrix.
    /// </remarks>
    [Fact]
    public void RunTheWholeEndToEndSuiteAcrossTheLegsItWaitsFor()
    {
        var job = JobLines(EndToEnd);

        var matrix = job.FindIndex(line => line.Trim() == "matrix:");
        matrix.Should().BeGreaterThan(-1,
            $"{EndToEnd} runs the suite in shares, a leg of a matrix each, so that a pull request " +
            "does not wait for one browser working through every spec in turn");

        var dimensions = job
            .Skip(matrix + 1)
            .TakeWhile(line => line.Trim().Length == 0 || IndentOf(line) > IndentOf(job[matrix]))
            .Where(line => line.Trim().Length > 0)
            .Select(line => line.Trim())
            .ToList();

        dimensions.Should().ContainSingle(
            "strategy.job-total counts every leg of the matrix, and a dimension beside the shard " +
            "numbers makes the count larger than they are, so the shares past them are never run");

        var legs = ShardLegs.Match(dimensions[0]);
        legs.Success.Should().BeTrue(
            $"the legs are read from the inline list \"shard: [1, 2, 3]\", and {dimensions[0]} is not one");

        var numbers = legs.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(number => int.TryParse(number, out var value) ? value : -1)
            .ToArray();

        numbers.Should().HaveCountGreaterThan(1, "a suite in one share is the job that was too long");
        numbers.Should().Equal(Enumerable.Range(1, numbers.Length),
            "Playwright numbers the shares from one to their count, and a number missing from " +
            "the list is a share no leg runs");

        var runs = job
            .Where(line => line.Contains("npm run test:e2e:fast", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .ToList();

        runs.Should().ContainSingle($"{EndToEnd} runs the suite in one step");

        var share = ShardFlag.Match(runs[0]);
        share.Success.Should().BeTrue(
            "every leg hands the suite its own number out of the count of legs, " +
            "--shard=${{ matrix.shard }}/${{ strategy.job-total }}; without it every leg runs the " +
            $"whole suite, and {runs[0]} is what this leg runs");

        var count = share.Groups[1].Value;
        if (count.All(char.IsAsciiDigit))
        {
            int.Parse(count).Should().Be(numbers.Length,
                "a count of shares written down beside the list of legs has to be the length of " +
                "that list, or some share is run by no leg");
        }
    }

    /// <remarks>
    /// Compose pins all four images to one tag, so a publisher pushes one immutable tag and the promotion
    /// job moves the names by digest.
    /// </remarks>
    [Fact]
    public void GiveTheMovingNamesOnceAndAfterEveryPublisher()
    {
        var workflow = File.ReadAllText(Path.Combine(RepositoryLayout.Root, Workflow));
        var jobs = Jobs();

        jobs.Should().ContainKey(Promotion,
            "without it the moving names are set by each publisher for itself");

        var publishers = jobs.Keys
            .Where(name => name.StartsWith("publish", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        publishers.Should().NotBeEmpty("the rule is about the jobs that push images");
        jobs[Promotion].OrderBy(name => name, StringComparer.Ordinal).Should().Equal(publishers,
            "a name moved before the last image is pushed names a release that does not exist yet");

        // The moving names, in the vocabulary the metadata action spells them.
        foreach (var moving in new[] { "type=raw,value=latest", "type=ref,event=branch", "type=ref,event=tag" })
        {
            workflow.Should().NotContain(moving,
                $"{moving} in a publisher sets a shared name from one leg of a matrix, and the " +
                "legs finish at different moments");
        }

        workflow.Should().Contain("type=sha,prefix=",
            "the tag a publisher does push has to name the commit it was built from");
    }

    [Fact]
    public void ReachTheSecurityScansThroughTheGraph()
    {
        var workflow = File.ReadAllText(Path.Combine(RepositoryLayout.Root, Workflow));
        var security = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, ".github", "workflows", "security.yml"));

        workflow.Should().Contain("uses: ./.github/workflows/security.yml",
            "a job in another workflow cannot be named in needs, so it has to be called");
        security.Should().Contain("workflow_call:",
            "and the called workflow has to accept being called");
        security.Should().NotContain("  push:",
            "triggered on push as well, it would run twice for every change and the " +
            "second run would gate nothing");
    }
}
