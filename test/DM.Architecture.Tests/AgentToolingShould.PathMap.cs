using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Which checks a change needs is decided once, by one map, for CI and for the
/// machine that pushes.
/// </summary>
/// <remarks>
/// scripts/ci-paths.mjs is the one reader of scripts/ci-paths.json, for CI, the hook and the
/// gates script alike. The rules below hold the places where a skip would be silent.
/// </remarks>
public partial class AgentToolingShould
{
    private const string PathMap = "scripts/ci-paths.mjs";

    private static readonly TimeSpan MapTimeout = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Jobs of the .NET workflow the map does not gate, with the reason each.
    /// </summary>
    /// <remarks>Every other job is the map's to call; a row naming no job fails.</remarks>
    private static readonly Dictionary<string, string> JobsOutsideTheMap = new(StringComparer.Ordinal)
    {
        ["changes"] = "is where the map is read; every gated job waits for it",
        ["security"] = "a call into security.yml, whose two jobs are gated there by the inputs this passes",
        ["publish"] = "publishes on its own conditions, and lets a check the map skipped through",
        ["publish-frontend"] = "the same",
        ["promote-images"] = "moves the names after the publishers, and checks only what they attested",
    };

    private static readonly Regex NeedsOfJob = new(@"^    needs:\s*\[([^\]]*)\]", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>The jobs of the map, in the order it declares them.</summary>
    private static string[] MapJobs()
    {
        using var map = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryLayout.Root, "scripts", "ci-paths.json")));

        return map.RootElement.GetProperty("jobs").EnumerateObject().Select(job => job.Name).ToArray();
    }

    /// <summary>The jobs of one workflow, each with its lines, comments out.</summary>
    private static Dictionary<string, string> JobBodies(string workflow) => WorkflowJobs.Read(workflow)
        .ToDictionary(
            job => job.Name,
            job => string.Concat(job.Lines.Where(WorkflowJobs.IsCode).Select(line => line + Environment.NewLine)),
            StringComparer.Ordinal);

    /// <summary>The code of a file: its lines, the ones that are comments out.</summary>
    private static string ShellCode(string path, string comment = "#") => string.Join("\n", ShellLines(path, comment));

    private static IEnumerable<string> ShellLines(string path, string comment = "#") => File.ReadAllLines(path)
        .Where(line => !line.TrimStart().StartsWith(comment, StringComparison.Ordinal));

    [Fact]
    public void ReadOneMapOfPaths()
    {
        var hook = ShellCode(Path.Combine(RepositoryLayout.Root, "scripts", "hooks", "pre-push"));
        var gates = ShellCode(Path.Combine(RepositoryLayout.Root, "scripts", "gates.sh"));
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
            .SelectMany(workflow => ShellLines(WorkflowPath(workflow))
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
    /// With the implicit success() a narrowed push would publish nothing, and failure() alone
    /// lets an image out past a cancelled check.
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
    /// <remarks>DM.Architecture.Tests and the client unit specs both read docs/.</remarks>
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
    /// <remarks>The map answers "false" for a name it does not have, so a misspelt job skips its gates.</remarks>
    [Fact]
    public void AskTheMapOnlyAboutItsOwnJobs()
    {
        var map = MapJobs().Append("always").ToHashSet(StringComparer.Ordinal);
        var hook = ShellCode(Path.Combine(RepositoryLayout.Root, "scripts", "hooks", "pre-push"));
        var gates = ShellCode(Path.Combine(RepositoryLayout.Root, "scripts", "gates.sh"));

        var asked = Regex.Matches(hook, @"\bwants ([a-z][a-z0-9-]*)")
            .Select(match => ("pre-push", match.Groups[1].Value))
            .Concat(Regex.Matches(gates, @"^\s*step (?:""([a-z0-9 -]+)""|([a-z0-9-]+)) ", RegexOptions.Multiline)
                .SelectMany(match => (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(job => ("gates.sh", job)))
            .ToList();

        asked.Should().Contain(question => question.Item1 == "pre-push", "the hook asks the map before its gates");
        asked.Should().Contain(question => question.Item1 == "gates.sh", "and so does scripts/gates.sh");
        Named(asked.Where(question => !map.Contains(question.Item2)).Select(question => $"{question.Item1}: {question.Item2}"))
            .Should().BeEmpty("the map answers false for a job it does not have, and the gates behind it never run again");
    }

    /// <summary>The one function both scripts ask whether the full set was asked for.</summary>
    private const string FullRequest = "full_requested";

    /// <summary>A condition that holds only when the full set was asked for: the request, not negated.</summary>
    private static readonly Regex AsksForTheFullSet = new(
        @"(?<!!\s*)\b" + FullRequest + @"\b", RegexOptions.Compiled);

    /// <summary>The first line of a function written over several lines; a lone "}" closes it.</summary>
    private static readonly Regex FunctionStart = new(@"^([a-z_]+)\(\)\s*\{$", RegexOptions.Compiled);

    /// <summary>The check of the stand, which GateSyntax leaves out: no workflow runs it.</summary>
    private static readonly Regex StandCheck = new(@"check-e2e-stand\.sh", RegexOptions.Compiled);

    /// <summary>
    /// The gates a push of a branch runs, each as the start of its command with
    /// the root and the quotes dropped.
    /// </summary>
    /// <remarks>The gates that answer in seconds or minutes and name what broke.</remarks>
    private static readonly string[] GatesOfABranchPush =
    [
        "block-dangerous-git.test.js",
        "block-new-migrations.test.js",
        "lint-edited-file.test.js",
        "block-in-subagents.test.js",
        "lint-agent-files.test.js",
        "lint-size-hint.test.js",
        "ci-paths.mjs",
        "npm ci --dry-run",
        "compare-code.test.mjs",
        "npm run test:conventions",
        "npm run knip",
        "npm run lint:ci",
        "npm run type-check",
        "node_modules/.bin/vue-tsc",
        "node_modules/.bin/eslint",
        "dotnet format",
        "dotnet build",
        "dotnet test test/DM.Architecture.Tests",
    ];

    /// <summary>
    /// A push of a branch runs the gates that answer first, and every other gate
    /// waits until the full set is asked for, whatever the map says.
    /// </summary>
    /// <remarks>
    /// A branch reaches dev only through a pull request, where CI runs every gate; the rest runs on
    /// DM_GATES_FULL. Read from the if blocks a gate stands in, and held both ways.
    /// </remarks>
    [Theory]
    [InlineData("hooks/pre-push")]
    [InlineData("gates.sh")]
    public void RunOnABranchOnlyTheGatesThatAnswerFirst(string script)
    {
        var code = ShellLines(Path.Combine(RepositoryLayout.Root, "scripts", script))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        var request = code.FirstOrDefault(line => line.StartsWith($"{FullRequest}()", StringComparison.Ordinal));
        request.Should().NotBeNull($"scripts/{script} asks whether the full set was asked for in one place, {FullRequest}");
        request.Should().Contain("DM_GATES_FULL", "the switch that asks for the full set is the one that runs it");

        var gates = GatesByGuard(code).ToList();
        var onTheBranch = gates.Where(gate => !gate.Guarded).Select(gate => gate.Command).Distinct().ToList();

        gates.Should().Contain(gate => gate.Guarded && gate.Command.StartsWith("npm run test:e2e", StringComparison.Ordinal),
            $"scripts/{script} holds the full set too, the end-to-end suite last");
        Named(onTheBranch.Where(gate => !GatesOfABranchPush.Any(cheap => gate.StartsWith(cheap, StringComparison.Ordinal))))
            .Should().BeEmpty(
                $"scripts/{script} runs these on every push of a branch, and CI runs them on the pull request: " +
                $"put them behind {FullRequest}, or name them in GatesOfABranchPush, see the remarks");
        Named(GatesOfABranchPush.Where(cheap => !onTheBranch.Any(gate => gate.StartsWith(cheap, StringComparison.Ordinal))))
            .Should().BeEmpty(
                $"scripts/{script} no longer runs these on a push of a branch, so the push loses the answer that comes " +
                "in minutes and waits for the pull request to give it");
    }

    /// <summary>Every gate a script runs, and whether a request for the full set guards it.</summary>
    private static IEnumerable<(string Command, bool Guarded)> GatesByGuard(List<string> code)
    {
        var functions = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var top = new List<string>();
        List<string>? body = null;
        foreach (var line in code)
        {
            var start = FunctionStart.Match(line);
            if (body is null && start.Success)
            {
                functions[start.Groups[1].Value] = body = new List<string>();
            }
            else if (body is not null && line == "}")
            {
                body = null;
            }
            else if (body is not null)
            {
                body.Add(line);
            }
            else
            {
                top.Add(line);
            }
        }

        var guards = new Stack<string>();
        foreach (var line in top)
        {
            if (line.StartsWith("if ", StringComparison.Ordinal) && !line.EndsWith("fi", StringComparison.Ordinal))
            {
                guards.Push(line);
            }
            else if (line.StartsWith("elif ", StringComparison.Ordinal) || line == "else")
            {
                guards.Pop();
                guards.Push(line);
            }
            else if (line == "fi")
            {
                guards.Pop();
            }

            var called = functions
                .Where(function => Regex.IsMatch(line, $@"\b{function.Key}\b"))
                .SelectMany(function => function.Value);
            foreach (var gate in called.Prepend(line).SelectMany(GatesOfLine))
            {
                yield return (gate, AsksForTheFullSet.IsMatch(line) || guards.Any(AsksForTheFullSet.IsMatch));
            }
        }
    }

    /// <summary>The gates one line runs, with the root and the quotes dropped.</summary>
    private static IEnumerable<string> GatesOfLine(string line) => GateSyntax.Matches(line)
        .Concat(StandCheck.Matches(line))
        .Select(match => match.Value.Trim()
            .Replace("$ROOT/", "", StringComparison.Ordinal)
            .Replace("\"", "", StringComparison.Ordinal));

    /// <summary>
    /// The decisions of the map, one change at a time.
    /// </summary>
    /// <remarks>A dry run of the script itself, the one CI and the hook run.</remarks>
    [Theory]
    [InlineData("docs/guides/TESTING.md|README.md|.claude/agents/debugger.md", "docs")]
    [InlineData("src/DM.Web.Client/src/App.vue|src/DM.Web.Client/package.json",
        "codeql dependency-scan deployment-smoke docs e2e frontend")]
    [InlineData("src/DM.Web.API/Program.cs", "build codeql dependency-scan deployment-smoke e2e frontend zap-scan")]
    [InlineData("src/DM.Web.Client/src/assets/styles/ThemeVariables.css",
        "build codeql dependency-scan deployment-smoke e2e frontend")]
    [InlineData("src/DM.Web.Client/nginx.conf",
        "build codeql compose-topology dependency-scan deployment-smoke e2e frontend zap-scan")]
    [InlineData("src/DM.Web.Client/publish-assets.sh",
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
    /// Only the build job runs those tests. Found by Path.Combine over "docs" or over "src",
    /// "DM.Web.Client"; DM.Architecture.Tests is left out, the docs job runs it.
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
            "OpenApiParametersShould reads it, and a walk that misses it holds nothing");
        read.Should().Contain(path => path.StartsWith("src/DM.Web.Client/", StringComparison.Ordinal),
            "the markup tests read the tables of the client, and a walk that misses them holds nothing");

        Named(read.Where(path => !Running(AskTheMap(path).Output).Split(' ').Contains("build")))
            .Should().BeEmpty("a server test reads these, so a change to one of them has to call the build job");
    }

    /// <summary>
    /// A file of the source tree the deployment checks parse is sorted where
    /// compose-topology runs.
    /// </summary>
    /// <remarks>Found by the src/ paths the scripts of scripts/deployment name.</remarks>
    [Fact]
    public void SortTheFilesTheDeploymentChecksReadWhereTheyRun()
    {
        var read = Directory.EnumerateFiles(Path.Combine(RepositoryLayout.Root, "scripts", "deployment"), "*.sh")
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
        return Directory.EnumerateFiles(Path.Combine(RepositoryLayout.Root, "test"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                           && !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                           && !file.Contains("DM.Architecture.Tests", StringComparison.Ordinal));
    }

    /// <summary>A file as itself, a folder as every file under it, repository-relative.</summary>
    private static IEnumerable<string> TrackedUnder(string path)
    {
        var full = Path.Combine(RepositoryLayout.Root, path);
        return Directory.Exists(full)
            ? Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(RepositoryLayout.Root, file).Replace('\\', '/'))
            : new[] { path };
    }

    /// <summary>The jobs the map runs, from its stdout, in order of name.</summary>
    private static string Running(string output) => string.Join(' ', output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(line => line.EndsWith("=true", StringComparison.Ordinal))
        .Select(line => line[..line.IndexOf('=')])
        .OrderBy(job => job, StringComparer.Ordinal));

    /// <summary>Runs the map on the given paths, one per line, at the repository root.</summary>
    /// <remarks>The switch that forces the full set belongs to whoever runs the gates: inherited, it gives every case one answer.</remarks>
    private static (int ExitCode, string Output, string Error) AskTheMap(string paths) =>
        RepositoryFiles.Run("node", $"{PathMap} --stdin", paths, MapTimeout, "DM_GATES_FULL");
}
