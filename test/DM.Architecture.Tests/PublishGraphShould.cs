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
    /// job moves the names by digest the run built.
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

    private static string[] Publishers() => Jobs().Keys
        .Where(name => name.StartsWith("publish", StringComparison.OrdinalIgnoreCase))
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    /// <remarks>
    /// A release tag is made on a commit dev already built and attested; a run on the tag would
    /// build a second set of digests that nobody signed.
    /// </remarks>
    [Fact]
    public void RunOnBranchesAndTheScheduleOnly()
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryLayout.Root, Workflow));

        var push = Array.FindIndex(lines, line => line == "  push:");
        push.Should().BeGreaterThan(-1, "the workflow publishes on a push");

        var trigger = lines
            .Skip(push + 1)
            .TakeWhile(line => line.Trim().Length == 0 || IndentOf(line) > 2)
            .Where(WorkflowJobs.IsCode);

        trigger.Should().NotContain(line => line.TrimStart().StartsWith("tags:", StringComparison.Ordinal),
            "a push of a tag would build the release's images again");

        string.Join("\n", lines.Where(WorkflowJobs.IsCode)).Should().NotContain("refs/tags",
            "with no tag trigger a condition on a tag ref is a branch nobody takes");
    }

    /// <remarks>
    /// A writer's run on dev can write the cache, and the next clean commit would ship its layers
    /// under a correct attestation.
    /// </remarks>
    [Fact]
    public void BuildThePublishedImagesWithoutAnImportedCache()
    {
        foreach (var publisher in Publishers())
        {
            var body = string.Join("\n", JobLines(publisher));

            body.Should().NotContain("cache-from", $"{publisher} builds what it attests from the commit alone");
            body.Should().NotContain("cache-to", $"{publisher} would leave a cache the next build could be told to read");
        }
    }

    [Fact]
    public void AttestEveryImageItPublishes()
    {
        var publishers = Publishers();
        publishers.Should().NotBeEmpty("the rule is about the jobs that push images");

        foreach (var publisher in publishers)
        {
            // The dash of a list item off, so that a key reads the same first in a step or later.
            var lines = JobLines(publisher).Select(line => line.Trim().TrimStart('-').TrimStart()).ToList();

            lines.Should().Contain("id-token: write",
                $"{publisher} signs the attestation with the identity of the run");
            lines.Should().Contain("attestations: write",
                $"{publisher} stores the attestation where gh attestation verify reads it");
            lines.Should().Contain(line => line.StartsWith("uses: actions/attest-build-provenance@", StringComparison.Ordinal),
                $"{publisher} has to attest what it pushed");
            lines.Should().Contain("subject-digest: ${{ steps.build.outputs.digest }}",
                $"{publisher} attests the digest its build step pushed, not a tag that can move");
            lines.Should().Contain(line => line.StartsWith("uses: actions/upload-artifact@", StringComparison.Ordinal),
                $"{publisher} hands its digest to the promotion and to the release command");
            lines.Should().Contain(line => line.StartsWith("name: digest-", StringComparison.Ordinal),
                $"the artifact of {publisher} is found by the name digest-<image>");
        }
    }

    [Fact]
    public void PromoteTheDigestsTheRunBuilt()
    {
        var body = string.Join("\n", JobLines(Promotion));

        body.Should().Contain("pattern: digest-*",
            "the promotion moves the digests this run's publishers recorded");
        body.Should().MatchRegex(
            @"bash scripts/deployment/promote-images\.sh ""[^""]+"" ""\$GITHUB_REF"" ""\$GITHUB_SHA""",
            "the script checks every attestation against the ref and the commit of this run");
        body.Should().NotContain("imagetools",
            "a name moved outside the script is moved without the checks");
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
