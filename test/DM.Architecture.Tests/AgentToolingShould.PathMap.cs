using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Which checks a change needs is decided once, by one map, for CI and for the
/// machine that pushes.
/// </summary>
/// <remarks>
/// A push used to run every gate whatever it touched, and a change to one
/// document paid for nineteen thousand tests and an end-to-end tier. The map in
/// scripts/ci-paths.json sorts the changed paths into groups and names the jobs
/// each group needs; scripts/ci-paths.mjs is the one reader of it, and the
/// changes job of the workflow, the pre-push hook and scripts/gates.sh all call
/// that reader. Two copies of the map would drift in the worst direction there
/// is: the hook skipping what CI runs is a push that finds out, and CI skipping
/// what the hook ran is a check nothing enforces.
///
/// A skipped job is the cheap outcome only while it is a decision. The rules
/// below hold the parts where a skip would be silent instead: a job of the
/// workflow the map does not know, a job name the hook misspells (its gates
/// would never run again), a document or a client file a server test reads
/// sorted where the build job does not run, a file the deployment checks parse
/// sorted where they do not run, and the decisions themselves, which a dry run pins down case by case.
/// </remarks>
public partial class AgentToolingShould
{
    private const string PathMap = "scripts/ci-paths.mjs";

    private static readonly TimeSpan MapTimeout = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Jobs of the .NET workflow the map does not gate, with the reason each.
    /// </summary>
    /// <remarks>
    /// Every other job is the map's to call. A row naming a job that no longer
    /// exists fails the rule, the way a stale row of the registers above does.
    /// </remarks>
    private static readonly Dictionary<string, string> JobsOutsideTheMap = new(StringComparer.Ordinal)
    {
        ["changes"] = "is where the map is read; every gated job waits for it",
        ["security"] = "a call into security.yml, whose two jobs are gated there by the inputs this passes",
        ["publish"] = "publishes on its own conditions, and lets a check the map skipped through",
        ["publish-frontend"] = "the same",
        ["promote-images"] = "moves the names after the publishers, and checks nothing",
    };

    private static readonly Regex NeedsOfJob = new(@"^    needs:\s*\[([^\]]*)\]", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>The jobs of the map, in the order it declares them.</summary>
    private static string[] MapJobs()
    {
        using var map = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot, "scripts", "ci-paths.json")));

        return map.RootElement.GetProperty("jobs").EnumerateObject().Select(job => job.Name).ToArray();
    }

    /// <summary>The jobs of one workflow, each with its lines, comments out.</summary>
    private static Dictionary<string, string> JobBodies(string workflow)
    {
        var jobs = new Dictionary<string, string>(StringComparer.Ordinal);
        var body = new StringBuilder();
        string? job = null;
        var inside = false;

        foreach (var line in File.ReadAllLines(WorkflowPath(workflow))
                     .Where(line => !line.TrimStart().StartsWith("#", StringComparison.Ordinal)))
        {
            inside |= line.StartsWith("jobs:", StringComparison.Ordinal);
            var header = inside ? JobHeader.Match(line) : Match.Empty;
            if (header.Success)
            {
                if (job != null)
                {
                    jobs[job] = body.ToString();
                }

                job = header.Groups[1].Value;
                body.Clear();
            }
            else if (job != null)
            {
                body.AppendLine(line);
            }
        }

        if (job != null)
        {
            jobs[job] = body.ToString();
        }

        return jobs;
    }

    /// <summary>The code of a file: its lines, the ones that are comments out.</summary>
    private static string ShellCode(string path, string comment = "#") => string.Join("\n", File.ReadAllLines(path)
        .Where(line => !line.TrimStart().StartsWith(comment, StringComparison.Ordinal)));

    [Fact]
    public void ReadOneMapOfPaths()
    {
        var hook = ShellCode(Path.Combine(RepositoryRoot, "scripts", "hooks", "pre-push"));
        var gates = ShellCode(Path.Combine(RepositoryRoot, "scripts", "gates.sh"));
        var changes = JobBodies("dotnet.yml").GetValueOrDefault("changes");

        changes.Should().NotBeNull("dotnet.yml decides in its changes job which jobs a change needs");
        foreach (var (reader, code) in new[] { ("the hook", hook), ("scripts/gates.sh", gates), ("the changes job", changes!) })
        {
            code.Should().Contain(PathMap,
                $"{reader} has to ask the one map which checks a change needs, or it decides by a second one");
        }

        changes.Should().Contain(">> \"$GITHUB_OUTPUT\"",
            "the decisions reach the other jobs as outputs of this one");
        changes.Should().Contain("--summary \"$GITHUB_STEP_SUMMARY\"",
            "what the map skipped and why is written on the run page, or a skip is a silence");

        // A second map in another syntax: a trigger filter, a filtering action,
        // or a diff read and sorted by the caller itself.
        var secondMaps = Workflows
            .SelectMany(workflow => File.ReadAllLines(WorkflowPath(workflow))
                .Where(line => !line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                .Where(line => Regex.IsMatch(line, @"^\s+paths(?:-ignore)?:|paths-filter"))
                .Select(line => $"{workflow}: {line.Trim()}"))
            .Concat(new[] { ("pre-push", hook), ("gates.sh", gates) }
                .Where(script => script.Item2.Contains("git diff", StringComparison.Ordinal))
                .Select(script => $"{script.Item1}: git diff"));

        Named(secondMaps).Should().BeEmpty(
            $"which paths need which checks is written once, in scripts/ci-paths.json, and read by {PathMap}");
    }

    [Fact]
    public void GateEveryJobByTheMap()
    {
        var map = MapJobs();
        var build = JobBodies("dotnet.yml");
        var security = JobBodies("security.yml");
        map.Should().NotBeEmpty("the map names the jobs it decides about");

        var problems = new List<string>();
        foreach (var job in map)
        {
            if (build.TryGetValue(job, out var body))
            {
                if (!body.Contains($"if: needs.changes.outputs.{job} == 'true'", StringComparison.Ordinal))
                    problems.Add($"dotnet.yml {job}: no condition on needs.changes.outputs.{job}");
                if (!NeedsOfJob.Match(body).Groups[1].Value.Split(',').Select(need => need.Trim()).Contains("changes"))
                    problems.Add($"dotnet.yml {job}: does not wait for changes, so its condition reads nothing");
            }
            else if (security.TryGetValue(job, out var scan))
            {
                if (!scan.Contains($"inputs.{job}", StringComparison.Ordinal))
                    problems.Add($"security.yml {job}: no condition on inputs.{job}");
                if (!build["security"].Contains($"{job}: ${{{{ needs.changes.outputs.{job} == 'true' }}}}", StringComparison.Ordinal))
                    problems.Add($"dotnet.yml security: does not hand {job} the map's decision");
            }
            else
            {
                problems.Add($"{job}: in the map and in neither workflow");
            }

            if (!build["changes"].Contains($"{job}: ${{{{ steps.select.outputs.{job} }}}}", StringComparison.Ordinal))
                problems.Add($"dotnet.yml changes: no output {job}");
        }

        problems.AddRange(build.Keys.Concat(security.Keys)
            .Where(job => !map.Contains(job) && !JobsOutsideTheMap.ContainsKey(job))
            .Select(job => $"{job}: a job the map does not call and JobsOutsideTheMap does not explain"));
        problems.AddRange(JobsOutsideTheMap.Keys
            .Where(job => !build.ContainsKey(job))
            .Select(job => $"JobsOutsideTheMap {job}: no such job in dotnet.yml"));

        Named(problems).Should().BeEmpty(
            "a job the map decides about runs exactly when the map says, and every other job says why it is outside it");
    }

    /// <summary>
    /// A check the map skipped does not stop publishing, and a check that failed
    /// or was cancelled still does.
    /// </summary>
    /// <remarks>
    /// GitHub skips a job whose need was skipped unless its condition says
    /// otherwise, so with the implicit success() every push the map narrowed
    /// would publish nothing, and nothing on the run page would call that a
    /// failure. failure() alone is the opposite hole: a check cancelled by
    /// concurrency is not a failure, and the image would go out past it.
    /// </remarks>
    [Fact]
    public void PublishPastWhatTheMapSkippedAndNothingElse()
    {
        var publishers = JobBodies("dotnet.yml").Where(job => job.Key.StartsWith("publish", StringComparison.Ordinal)).ToList();
        publishers.Should().NotBeEmpty("the rule is about the jobs that push images");

        var conditions = new[] { "!cancelled()", "!contains(needs.*.result, 'failure')", "!contains(needs.*.result, 'cancelled')" };
        Named(publishers
                .SelectMany(job => conditions
                    .Where(condition => !job.Value.Contains(condition, StringComparison.Ordinal))
                    .Select(condition => $"{job.Key}: {condition}")))
            .Should().BeEmpty("a publisher waits for every check, and a check the map skipped is not a check that failed");
    }

    /// <summary>
    /// The job a change to the documents calls runs both suites that read them.
    /// </summary>
    /// <remarks>
    /// DM.Architecture.Tests holds the documents to the tree, and the client
    /// unit specs resolve every file a comment names and parse SURFACES.md and
    /// BBCODE_RENDERING.md as data. Without either, a rename in docs/ lands
    /// green and reddens the next push that happens to run it.
    /// </remarks>
    [Fact]
    public void RunBothSuitesThatReadTheDocuments()
    {
        var docs = JobBodies("dotnet.yml").GetValueOrDefault("docs");

        docs.Should().NotBeNull("the map calls a docs job for a change to the documents");
        docs.Should().Contain("dotnet test test/DM.Architecture.Tests",
            "the architecture tests are the rules about the documents");
        docs.Should().Contain("npm run test:coverage",
            "the client unit specs read the documents too");
    }

    /// <summary>
    /// Every job the hook and scripts/gates.sh ask the map about is a job of the map.
    /// </summary>
    /// <remarks>
    /// The map answers "false" for a name it does not have, so a misspelt job in
    /// either script skips its gates on every push from then on, green.
    /// </remarks>
    [Fact]
    public void AskTheMapOnlyAboutItsOwnJobs()
    {
        var map = MapJobs().Append("always").ToHashSet(StringComparer.Ordinal);
        var hook = ShellCode(Path.Combine(RepositoryRoot, "scripts", "hooks", "pre-push"));
        var gates = ShellCode(Path.Combine(RepositoryRoot, "scripts", "gates.sh"));

        var asked = Regex.Matches(hook, @"\bwants ([a-z][a-z0-9-]*)")
            .Select(match => ("pre-push", match.Groups[1].Value))
            .Concat(Regex.Matches(gates, @"^step (?:""([a-z0-9 -]+)""|([a-z0-9-]+)) ", RegexOptions.Multiline)
                .SelectMany(match => (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(job => ("gates.sh", job)))
            .ToList();

        asked.Should().Contain(question => question.Item1 == "pre-push", "the hook asks the map before its gates");
        asked.Should().Contain(question => question.Item1 == "gates.sh", "and so does scripts/gates.sh");
        Named(asked.Where(question => !map.Contains(question.Item2)).Select(question => $"{question.Item1}: {question.Item2}"))
            .Should().BeEmpty("the map answers false for a job it does not have, and the gates behind it never run again");
    }

    /// <summary>
    /// The decisions of the map, one change at a time.
    /// </summary>
    /// <remarks>
    /// A dry run of the script itself rather than a copy of its logic here: the
    /// cases are what has to stay true of the map, and the thing they are held
    /// against is the one CI and the hook run.
    /// </remarks>
    [Theory]
    [InlineData("docs/guides/TESTING.md|README.md|.claude/agents/debugger.md", "docs")]
    [InlineData("src/DM.Web.Client/src/App.vue|src/DM.Web.Client/package.json",
        "codeql dependency-scan deployment-smoke docs e2e frontend")]
    [InlineData("src/DM.Web.API/Program.cs", "build codeql dependency-scan deployment-smoke e2e frontend zap-scan")]
    [InlineData("src/DM.Web.Client/src/assets/styles/ThemeVariables.css",
        "build codeql dependency-scan deployment-smoke e2e frontend")]
    [InlineData("src/DM.Web.Client/nginx.conf",
        "build codeql compose-topology dependency-scan deployment-smoke e2e frontend zap-scan")]
    [InlineData("docker/docker-compose.yml", "build codeql compose-topology dependency-scan deployment-smoke e2e frontend zap-scan")]
    [InlineData("scripts/hooks/pre-push", "build codeql compose-topology dependency-scan deployment-smoke e2e frontend zap-scan")]
    [InlineData("src/DM.Web.API/Program.cs|src/DM.Web.Client/src/App.vue",
        "build codeql compose-topology dependency-scan deployment-smoke e2e frontend zap-scan")]
    [InlineData("a-file-no-group-names.txt", "build codeql compose-topology dependency-scan deployment-smoke e2e frontend zap-scan")]
    [InlineData("", "")]
    public void RunWhatTheMapSaysAChangeNeeds(string paths, string expected)
    {
        var (exitCode, output, error) = AskTheMap(paths.Replace('|', '\n'));

        exitCode.Should().Be(0, $"{PathMap} answers every set of paths{Environment.NewLine}{error}");
        Running(output).Should().Be(expected, $"for the change [{paths}]{Environment.NewLine}{error}");
    }

    /// <summary>
    /// A document or a client file a server test reads is sorted where the build
    /// job runs, not with the prose or with the client.
    /// </summary>
    /// <remarks>
    /// Only the build job runs those tests, and a group that does not call it
    /// lets a change to that file land without the test that reads it, and
    /// reddens the next server push instead. OpenApiContractShould reads
    /// API_DESIGN.md this way, EmailThemeShould the styles of the SPA and the
    /// markup tests the tables of the client. Found by the spelling such a read
    /// takes, Path.Combine over "docs" or over "src", "DM.Web.Client" and the
    /// segments after it. A read that stops at a folder holds every file in it.
    /// DM.Architecture.Tests is left out: the docs job runs it for the client.
    /// </remarks>
    [Fact]
    public void SortTheFilesServerTestsReadWhereTheBuildRuns()
    {
        var read = ServerTestSources()
            .SelectMany(file => Regex.Matches(ShellCode(file, "//"),
                @"(""docs""|""src""\s*,\s*""DM\.Web\.Client"")((?:\s*,\s*""[^""]+"")+)"))
            .Select(match => string.Join('/', Regex.Matches(match.Value, @"""([^""]+)""")
                .Select(segment => segment.Groups[1].Value)))
            .Distinct()
            .SelectMany(TrackedUnder)
            .ToList();

        read.Should().Contain(path => path.EndsWith("/API_DESIGN.md", StringComparison.Ordinal),
            "OpenApiContractShould reads it, and a walk that misses it holds nothing");
        read.Should().Contain(path => path.StartsWith("src/DM.Web.Client/", StringComparison.Ordinal),
            "the markup tests read the tables of the client, and a walk that misses them holds nothing");

        Named(read.Where(path => !Running(AskTheMap(path).Output).Split(' ').Contains("build")))
            .Should().BeEmpty("a server test reads these, so a change to one of them has to call the build job");
    }

    /// <summary>
    /// A file of the source tree the deployment checks parse is sorted where
    /// compose-topology runs.
    /// </summary>
    /// <remarks>
    /// check-nginx-configuration.sh parses the nginx.conf of the SPA, and the
    /// client group would let a typo in it reach the push, the place the check
    /// was moved into the hook to keep it from. Found by the src/ paths the
    /// scripts of scripts/deployment name.
    /// </remarks>
    [Fact]
    public void SortTheFilesTheDeploymentChecksReadWhereTheyRun()
    {
        var read = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "scripts", "deployment"), "*.sh")
            .SelectMany(script => Regex.Matches(ShellCode(script), @"\bsrc/[\w./-]+\.\w+"))
            .Select(match => match.Value)
            .Distinct()
            .ToList();

        read.Should().Contain("src/DM.Web.Client/nginx.conf", "check-nginx-configuration.sh parses it, and a walk that misses it holds nothing");

        Named(read.Where(path => !Running(AskTheMap(path).Output).Split(' ').Contains("compose-topology")))
            .Should().BeEmpty("a deployment check parses these, so a change to one of them has to call compose-topology");
    }

    /// <summary>The C# files of every test project but DM.Architecture.Tests.</summary>
    private static IEnumerable<string> ServerTestSources()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "test"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                           && !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                           && !file.Contains("DM.Architecture.Tests", StringComparison.Ordinal));
    }

    /// <summary>A file as itself, a folder as every file under it, repository-relative.</summary>
    private static IEnumerable<string> TrackedUnder(string path)
    {
        var full = Path.Combine(RepositoryRoot, path);
        return Directory.Exists(full)
            ? Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(RepositoryRoot, file).Replace('\\', '/'))
            : new[] { path };
    }

    /// <summary>The jobs the map runs, from its stdout, in order of name.</summary>
    private static string Running(string output) => string.Join(' ', output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(line => line.EndsWith("=true", StringComparison.Ordinal))
        .Select(line => line[..line.IndexOf('=')])
        .OrderBy(job => job, StringComparer.Ordinal));

    /// <summary>Runs the map on the given paths, one per line, at the repository root.</summary>
    private static (int ExitCode, string Output, string Error) AskTheMap(string paths)
    {
        var start = new ProcessStartInfo("node", $"{PathMap} --stdin")
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardInput = true,
            // No preamble: a BOM in front of the first path is a path no group names.
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        // The switch that forces the full set belongs to whoever runs the gates,
        // and inherited here it would turn every case into the same answer.
        start.Environment.Remove("DM_GATES_FULL");

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("node could not be started at the repository root");

        process.StandardInput.Write(paths);
        process.StandardInput.Close();

        // Both pipes drained before the wait: a full pipe blocks the child nobody reads.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(MapTimeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"node {PathMap} did not finish in {MapTimeout}");
        }

        return (process.ExitCode, output.Result, error.Result);
    }
}
