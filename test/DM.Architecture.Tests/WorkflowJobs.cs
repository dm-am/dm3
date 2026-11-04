using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;

namespace DM.Architecture.Tests;

/// <summary>
/// The jobs of a workflow under .github/workflows, read once for every rule about them.
/// </summary>
internal static class WorkflowJobs
{
    /// <summary>A job: below jobs: it is the only key written at two spaces.</summary>
    private static readonly Regex Key = new(@"^  ([A-Za-z_][\w.-]*):\s*$", RegexOptions.Compiled);

    /// <summary>The full path of a workflow, by its file name.</summary>
    internal static string PathOf(string workflow) =>
        Path.Combine(RepositoryLayout.Root, ".github", "workflows", workflow);

    /// <summary>The jobs of a workflow in the order it declares them, each with the lines under its key.</summary>
    /// <param name="workflow">File name of the workflow</param>
    /// <returns>Name and lines of every job, comments included</returns>
    internal static IReadOnlyList<(string Name, string[] Lines)> Read(string workflow)
    {
        var lines = File.ReadAllLines(PathOf(workflow));
        var start = Array.FindIndex(lines, line => line.StartsWith("jobs:", StringComparison.Ordinal));
        start.Should().BeGreaterThan(-1, $"{workflow} must declare jobs");

        var jobs = new List<(string, string[])>();
        var body = new List<string>();
        string? name = null;
        foreach (var line in lines.Skip(start + 1))
        {
            var key = Key.Match(line);
            if (!key.Success)
            {
                body.Add(line);
                continue;
            }

            if (name != null)
            {
                jobs.Add((name, body.ToArray()));
            }

            name = key.Groups[1].Value;
            body.Clear();
        }

        if (name != null)
        {
            jobs.Add((name, body.ToArray()));
        }

        return jobs;
    }

    /// <summary>The lines under the key of one job.</summary>
    /// <param name="workflow">File name of the workflow</param>
    /// <param name="job">Name of the job</param>
    /// <returns>The lines of the job, comments included</returns>
    internal static string[] Body(string workflow, string job)
    {
        var found = Read(workflow).Where(entry => entry.Name == job).Select(entry => entry.Lines).FirstOrDefault();
        found.Should().NotBeNull($"{workflow} must still declare the {job} job");
        return found!;
    }

    /// <summary>A line of a workflow that is not a comment.</summary>
    internal static bool IsCode(string line) => !line.TrimStart().StartsWith('#');
}
