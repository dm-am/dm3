using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
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

    /// <summary>
    /// Jobs a publisher deliberately does not wait for.
    /// </summary>
    /// <remarks>
    /// Empty, and an empty set is the answer: every check in the file is a check
    /// that could stop a release. The set exists so that an exception has to be
    /// written down rather than made by omission.
    /// </remarks>
    private static readonly HashSet<string> NotAGate = new(StringComparer.Ordinal);

    /// <summary>A top-level job key: two spaces, a name, a colon, nothing after it.</summary>
    private static readonly Regex JobKey = new(@"^  ([a-z][a-z0-9-]*):\s*$", RegexOptions.Compiled);

    /// <summary>The needs list of the job it follows, in the inline form the file uses.</summary>
    private static readonly Regex NeedsList = new(@"^    needs:\s*\[([^\]]*)\]", RegexOptions.Compiled);

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !(Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                     Directory.Exists(Path.Combine(directory.FullName, "test"))))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }

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

        var publishers = jobs.Keys.Where(name => name.StartsWith("publish", StringComparison.Ordinal)).ToArray();
        publishers.Should().NotBeEmpty("the rule is about the jobs that push images");

        foreach (var publisher in publishers)
        {
            var expected = jobs.Keys
                .Where(name => name != publisher)
                .Where(name => !name.StartsWith("publish", StringComparison.Ordinal))
                .Where(name => !NotAGate.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal);

            jobs[publisher].OrderBy(name => name, StringComparer.Ordinal)
                .Should().Equal(expected,
                    $"{publisher} pushes an image, and a check it does not wait for is a " +
                    "check that cannot stop the release");
        }
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
