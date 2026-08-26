using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Nothing reaches the registry ahead of a check that could have stopped it.
/// </summary>
/// <remarks>
/// The two publishing jobs waited on four checks out of ten. Two of the missing
/// ones sat in the same file and were simply forgotten in <c>needs</c>; the other
/// two — CodeQL and the ZAP scan — lived in a workflow triggered beside this one,
/// where no <c>needs</c> can reach them at all. They ran on the same push and
/// finished whenever they finished, so an image with a red scan reached the
/// registry, took the <c>latest</c> tag on main, and watchtower installed it
/// within five minutes.
///
/// Written as "every other job in the file", not as a list. A list is the thing
/// that went stale: a job added tomorrow has to be named in <c>needs</c> or named
/// here as a deliberate exception, and there is nowhere else to put it.
/// </remarks>
public class PublishGraphShould
{
    private const string Workflow = ".github/workflows/dotnet.yml";

    /// <summary>The job that gives the images their moving names.</summary>
    private const string Promotion = "promote-images";

    /// <summary>
    /// Jobs a publisher deliberately does not wait for.
    /// </summary>
    /// <remarks>
    /// One entry, and it is the only shape that can ever be in here: a job that
    /// runs after the publishers rather than before them. Waiting for it would be
    /// a cycle, and it stops no release because there is nothing left to stop.
    ///
    /// Everything else in the file is a check that could, which is why the set is
    /// written down rather than inferred: an exception has to be typed out.
    /// </remarks>
    private static readonly HashSet<string> NotAGate = new(StringComparer.Ordinal) { Promotion };

    /// <summary>
    /// A top-level job key: two spaces, a name, a colon, nothing after it.
    /// </summary>
    /// <remarks>
    /// The character class is what YAML and GitHub allow in a job id, not what
    /// this file happens to use today. Written as lowercase-and-hyphen only, a
    /// job named <c>publish_docs</c> or <c>Publish</c> would be invisible to the
    /// rule — and a check the rule cannot see is a check the publishers are not
    /// held to waiting for, which is the whole defect this guards.
    /// </remarks>
    private static readonly Regex JobKey = new(@"^  ([A-Za-z_][A-Za-z0-9_-]*):\s*$", RegexOptions.Compiled);

    /// <summary>The needs list of the job it follows, in the inline form the file uses.</summary>
    private static readonly Regex NeedsList = new(@"^    needs:\s*\[([^\]]*)\]", RegexOptions.Compiled);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    /// <summary>Every job in the workflow, with the jobs each one waits for.</summary>
    private static IReadOnlyDictionary<string, string[]> Jobs()
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot, Workflow));
        var jobs = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var inJobs = false;
        string? current = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("jobs:", StringComparison.Ordinal))
            {
                inJobs = true;
                continue;
            }

            if (!inJobs)
            {
                continue;
            }

            var key = JobKey.Match(line);
            if (key.Success)
            {
                current = key.Groups[1].Value;
                jobs[current] = Array.Empty<string>();
                continue;
            }

            var needs = NeedsList.Match(line);
            if (needs.Success && current != null)
            {
                jobs[current] = needs.Groups[1].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToArray();
            }
        }

        return jobs;
    }

    [Fact]
    public void WaitForEveryCheckTheWorkflowRuns()
    {
        var jobs = Jobs();
        jobs.Should().HaveCountGreaterThan(5, "a reader that finds no jobs asserts nothing");

        var publishers = jobs.Keys.Where(name => name.StartsWith("publish", StringComparison.OrdinalIgnoreCase)).ToArray();
        publishers.Should().NotBeEmpty("the rule is about the jobs that push images");

        foreach (var publisher in publishers)
        {
            var expected = jobs.Keys
                .Where(name => name != publisher)
                .Where(name => !name.StartsWith("publish", StringComparison.OrdinalIgnoreCase))
                .Where(name => !NotAGate.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal);

            jobs[publisher].OrderBy(name => name, StringComparer.Ordinal)
                .Should().Equal(expected,
                    $"{publisher} pushes an image, and a check it does not wait for is a " +
                    "check that cannot stop the release");
        }
    }

    /// <summary>
    /// One tag means one commit, and the moving names are set once.
    /// </summary>
    /// <remarks>
    /// The publish matrix is three legs plus the frontend, and each used to set
    /// "latest", the branch name and the release tag for itself. They finish at
    /// different moments and any of them can fail, so those names ended up
    /// pointing at whatever mixture of commits the run happened to leave behind -
    /// while compose pins all four images to one tag on the promise that they came
    /// from a single commit, and nothing downstream ever checks.
    ///
    /// So a publisher pushes exactly one immutable tag, and the promotion job
    /// moves the names afterwards by digest. Nothing about that is visible in a
    /// green run: the registry is not read by any test, and the mixture is only
    /// found by whoever deploys it.
    /// </remarks>
    [Fact]
    public void GiveTheMovingNamesOnceAndAfterEveryPublisher()
    {
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot, Workflow));
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

    /// <summary>
    /// The security scans are part of this graph, not a workflow beside it.
    /// </summary>
    [Fact]
    public void ReachTheSecurityScansThroughTheGraph()
    {
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot, Workflow));
        var security = File.ReadAllText(
            Path.Combine(RepositoryRoot, ".github", "workflows", "security.yml"));

        workflow.Should().Contain("uses: ./.github/workflows/security.yml",
            "a job in another workflow cannot be named in needs, so it has to be called");
        security.Should().Contain("workflow_call:",
            "and the called workflow has to accept being called");
        security.Should().NotContain("  push:",
            "triggered on push as well, it would run twice for every change and the " +
            "second run would gate nothing");
    }
}
