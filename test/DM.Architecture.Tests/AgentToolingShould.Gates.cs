using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The gates of the pre-push hook, of scripts/gates.sh and of the workflows are one
/// set, written down in three places.
/// </summary>
/// <remarks>Each rule compares two of the spellings, so a gate added to one place only fails a test.</remarks>
public partial class AgentToolingShould
{
    /// <summary>
    /// How a gate looks written down, in a shell script or in a workflow step.
    /// </summary>
    /// <remarks>
    /// Digits belong in an npm script name ("test:e2e:fast"), and `npm ci` stops after its flags:
    /// dependency-scan writes `npm ci &amp;&amp; npm audit ...` in one step. The client's own vue-tsc
    /// and eslint, called by path, check the frame stand in scripts/visual, out of reach of the npm scripts.
    /// </remarks>
    private static readonly Regex GateSyntax = new(
        @"(npm run [a-z0-9:-]+|npm ci\b(?: --[a-z-]+)*|npm audit [^\r\n)]+|dotnet (?:format|build|test)[^\r\n)]*|check-vulnerable-packages\.sh|check-coverage\.sh|check-shell-scripts\.sh|check-compose-topology\.sh|check-nginx-configuration\.sh|check-server-installer\.sh|check-monitoring\.sh|block-dangerous-git\.test\.js|block-new-migrations\.test\.js|lint-edited-file\.test\.js|block-in-subagents\.test\.js|lint-agent-files\.test\.js|lint-size-hint\.test\.js|compare-code\.test\.mjs|ci-paths\.mjs|node_modules/\.bin/(?:vue-tsc|eslint) [^\r\n)]+)",
        RegexOptions.Compiled);

    /// <summary>The gate commands of a shell script, in the order it runs them.</summary>
    /// <remarks>Read as commands: the hook runs a gate at the top level, the script through a helper.</remarks>
    private static string[] GateCommands(string path)
    {
        // Comments are prose and name commands freely: both files warn in words
        // about running a second dotnet test alongside the gates.
        var code = ShellCode(path);

        return GateSyntax.Matches(code)
            .Select(match => match.Value.Trim())
            // Paths differ between the two files ($ROOT vs "$ROOT"); the gate is
            // the command, not how the root is spelled.
            .Select(command => command.Replace("\"$ROOT\"", "$ROOT").Replace("$ROOT/", ""))
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// scripts/gates.sh runs the same gates as the pre-push hook.
    /// </summary>
    /// <remarks>The hook protects a bare clone, the script runs on an untouched tree, so the list lives twice.</remarks>
    [Fact]
    public void MirrorTheHookInTheGatesScript()
    {
        var hook = GateCommands(Path.Combine(RepositoryLayout.Root, "scripts", "hooks", "pre-push"));
        var script = GateCommands(Path.Combine(RepositoryLayout.Root, "scripts", "gates.sh"));

        hook.Should().NotBeEmpty("the hook is the source of the list");
        script.Should().BeEquivalentTo(hook,
            "scripts/gates.sh exists to answer before the push what the hook answers during it");
    }

    private static readonly string[] Workflows = { "dotnet.yml", "security.yml" };

    private static string WorkflowPath(string workflow) =>
        Path.Combine(RepositoryLayout.Root, ".github", "workflows", workflow);

    /// <summary>
    /// Gates that exist locally and in no workflow, by design.
    /// </summary>
    /// <remarks>
    /// The tests of the local hooks and of the code comparison guard this machine only; the
    /// convention specs run inside test:coverage in CI.
    /// </remarks>
    private static readonly string[] LocalOnlyGates =
    {
        "block-dangerous-git.test.js",
        "block-new-migrations.test.js",
        "lint-edited-file.test.js",
        "block-in-subagents.test.js",
        "lint-agent-files.test.js",
        "lint-size-hint.test.js",
        "compare-code.test.mjs",
        "npm run test:conventions",
    };

    /// <summary>
    /// Actions whose use is never a gate on this machine, with the reason each.
    /// </summary>
    /// <remarks>A step that hands its work to an action does what the action does, so the decision is the action's.</remarks>
    private static readonly Dictionary<string, string> ActionsThatGateNothingLocally =
        new(StringComparer.Ordinal)
        {
            ["actions/checkout"] =
                "the runner starts with no tree; this machine is the tree",
            ["actions/setup-dotnet"] =
                "installs an SDK that is already installed here, and that the two name one " +
                "band is CompilerPolicyShould.PinTheSdkToTheBandContinuousIntegrationInstalls",
            ["actions/setup-node"] =
                "installs a Node that is already installed here, and that the two name one " +
                "version is FrontendToolchainShould.InstallTheSameNodeEverywhereItIsInstalled",
            ["actions/upload-artifact"] =
                "carries a file onto the run page because the machine that wrote it is about " +
                "to be destroyed; here the file stays on disk",
            ["docker/setup-buildx-action"] =
                "prepares the builder the publishing jobs push from",
            ["docker/login-action"] =
                "registry credentials, which exist only as GitHub secrets",
            ["docker/metadata-action"] =
                "computes the image tags of a push",
            ["docker/build-push-action"] =
                "builds an image and pushes it to the registry",
            ["github/codeql-action/init"] =
                "the analysis runs on GitHub's side",
            ["github/codeql-action/analyze"] =
                "the analysis runs on GitHub's side and reports into its security tab",
            ["zaproxy/action-baseline"] =
                "an hour of dynamic scanning against a stack stood up for it",
        };

    /// <summary>
    /// Steps the local gates deliberately do not run, with the reason each.
    /// </summary>
    /// <remarks>
    /// A step is either run before the push or written down here with its reason. Rows are keyed
    /// by the step name, and a row naming no step fails.
    /// </remarks>
    private static readonly Dictionary<string, string> StepsLeftToContinuousIntegration =
        new(StringComparer.Ordinal)
        {
            // build
            ["Restore dependencies"] =
                "a local build restores as it goes; CI splits it out so that the build step " +
                "after it can pass --no-restore on a checkout that has no packages at all",

            // e2e: the suite itself runs locally against the stand scripts/check-e2e-stand.sh found up.
            ["Generate the throwaway secrets compose refuses to start without"] =
                "invents the values compose declares with ${...:?}; a machine here has " +
                "docker/.env with values of its own",
            ["Overlay that turns rate limiting off and lets the preview origin in"] =
                "writes the compose overlay the end-to-end job runs under; the stand here " +
                "gets the same switch from dm.ps1 e2e or dm.sh e2e, and the hook refuses a " +
                "stand without it",
            ["Start the stack"] =
                "brings the whole stack up under docker; the hook never starts one, it " +
                "names the command that does",
            ["Wait for API health"] =
                "waits on the stack the step before it started; the hook asks the same " +
                "route once, in scripts/check-e2e-stand.sh",
            ["Seed the database"] =
                "fills a database the suite reads; the stand here is seeded already",
            ["Install dependencies"] =
                "npm ci into an empty checkout; a clone has node_modules, and that the lock " +
                "file still describes them is the gate the local set runs instead",
            ["Install Chromium"] =
                "downloads a browser",
            ["Flaky tests of this run"] =
                "writes the retry summary onto the run page",
            ["API logs on failure"] =
                "prints container logs, which are already at hand on this machine",

            // publish, publish-frontend, promote-images
            ["Verify project exists"] =
                "guards a typo in the matrix; the local build compiles the solution, which a " +
                "missing project makes impossible",
            ["Point the moving names at this commit"] =
                "moves tags inside the registry",

            // deployment-smoke
            ["Generate the stand credentials"] =
                "writes the htpasswd file the edge mounts on a server",
            ["Start the pair the installer and the unit deploy"] =
                "brings up the deployed pair, edge included",
            ["The edge answers where the deployment needs it to"] =
                "smoke test against the pair the step before it started",
            ["Only the application role may connect to the application database"] =
                "reads the privileges of the database that pair started",
            ["The nightly dump holds no outbox rows and restores with an empty outbox table"] =
                "dumps and restores the database that pair started",
            ["Logs on failure"] =
                "prints container logs of that pair",

            // codeql and zap-scan
            ["Build for analysis"] =
                "compiles so that CodeQL has something to trace; the same compile is already " +
                "a gate of the build job and is run locally from there",
            ["Start infrastructure with Docker Compose"] =
                "brings the stack up for the scanner",
            ["Build and start API"] =
                "the API the scanner talks to",
            ["Prepare ZAP workspace"] =
                "creates the files the scanner writes its report into",
            ["Cleanup"] =
                "tears that stack down again",
        };

    /// <summary>One step of one job, as the workflow writes it down.</summary>
    private sealed record Step(string Workflow, string Job, string Name, string[] Uses, string Body)
    {
        public string Where => $"{Workflow} {Job} / {Name}";
    }

    private static readonly Regex JobHeader = new(@"^  ([a-z][a-z0-9-]*):\s*$", RegexOptions.Compiled);

    private static readonly Regex StepsKey = new(@"^\s+steps:\s*$", RegexOptions.Compiled);

    /// <summary>
    /// The line that opens a step, as opposed to a list item inside one.
    /// </summary>
    /// <remarks>Keyed on the first key of a step: a run block can hold YAML of its own.</remarks>
    private static readonly Regex StepStart = new(
        @"^\s*- (?:name|uses|run|id|if|env|with|working-directory):", RegexOptions.Compiled);

    /// <summary>The steps of one workflow, in the order it declares them.</summary>
    private static List<Step> StepsOf(string workflow)
    {
        var steps = new List<Step>();
        var block = new List<string>();
        var job = string.Empty;
        var insideJobs = false;
        var insideSteps = false;
        var indent = -1;

        void Flush()
        {
            if (block.Count > 0)
            {
                steps.Add(ToStep(workflow, job, block, indent));
                block.Clear();
            }
        }

        // Comments are prose: these workflows explain in words what their steps
        // do, and a command named in an explanation is not a command that runs.
        var lines = ShellLines(WorkflowPath(workflow));

        foreach (var line in lines)
        {
            if (line.StartsWith("jobs:", StringComparison.Ordinal))
            {
                insideJobs = true;
                continue;
            }

            if (!insideJobs || line.Trim().Length == 0)
            {
                continue;
            }

            var header = JobHeader.Match(line);
            if (header.Success)
            {
                Flush();
                job = header.Groups[1].Value;
                insideSteps = false;
                indent = -1;
                continue;
            }

            if (StepsKey.IsMatch(line))
            {
                Flush();
                insideSteps = true;
                indent = -1;
                continue;
            }

            if (!insideSteps)
            {
                continue;
            }

            var here = line.Length - line.TrimStart().Length;
            if (StepStart.IsMatch(line) && (indent < 0 || here == indent))
            {
                Flush();
                indent = here;
            }

            block.Add(line);
        }

        Flush();
        return steps;
    }

    /// <summary>
    /// What a step is called and what it hands its work to.
    /// </summary>
    /// <remarks>Only the step's own keys: "name:" inside with: names an artifact.</remarks>
    private static Step ToStep(string workflow, string job, List<string> block, int indent)
    {
        var keys = new List<string> { block[0].TrimStart()[1..].Trim() };
        keys.AddRange(block
            .Skip(1)
            .Where(line => line.Length - line.TrimStart().Length == indent + 2)
            .Select(line => line.Trim()));

        string? Value(string key) => keys
            .FirstOrDefault(line => line.StartsWith(key + ":", StringComparison.Ordinal))
            ?[(key.Length + 1)..]
            .Trim();

        var uses = keys
            .Where(line => line.StartsWith("uses:", StringComparison.Ordinal))
            .Select(line => line["uses:".Length..].Trim())
            .ToArray();

        var run = Value("run");
        var name = Value("name")
                   ?? uses.FirstOrDefault()
                   ?? (run is null or "|" or ">" ? block[0].Trim() : run);

        return new Step(workflow, job, name, uses, string.Join("\n", block));
    }

    /// <summary>
    /// What a gate is, with the spelling of one runner dropped.
    /// </summary>
    /// <remarks>
    /// Flags differ on purpose between CI and the local files; what must match is which gates run.
    /// Coverage collection stays: it writes the report the coverage gate reads.
    /// </remarks>
    private static string GateIdentity(string command)
    {
        var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (command.StartsWith("npm run ", StringComparison.Ordinal))
        {
            return string.Join(' ', words.Take(3));
        }

        if (command.StartsWith("dotnet test", StringComparison.Ordinal))
        {
            return command.Contains("--coverage", StringComparison.Ordinal)
                ? "dotnet test --coverage"
                : "dotnet test";
        }

        if (words.Length >= 2 && (words[0] == "dotnet" || words[0] == "npm"))
        {
            return $"{words[0]} {words[1]}";
        }

        return command;
    }

    private static string[] Identities(IEnumerable<string> commands) =>
        commands.Select(GateIdentity).Distinct().OrderBy(gate => gate, StringComparer.Ordinal).ToArray();

    /// <summary>The gates one step of a workflow runs.</summary>
    private static string[] GatesOf(Step step) =>
        Identities(GateSyntax.Matches(step.Body).Select(match => match.Value.Trim()));

    /// <summary>
    /// Sorts the steps of the workflows into those whose gates the local files run
    /// and those nobody has accounted for.
    /// </summary>
    /// <remarks>An exempt step is left out, an action step needs its action listed, every other step runs local gates.</remarks>
    private static (List<Step> Mirrored, List<string> Unaccounted) Account(
        IEnumerable<Step> steps, IReadOnlyCollection<string> local)
    {
        var mirrored = new List<Step>();
        var unaccounted = new List<string>();

        foreach (var step in steps)
        {
            if (StepsLeftToContinuousIntegration.ContainsKey(step.Name))
            {
                continue;
            }

            if (step.Uses.Length > 0)
            {
                unaccounted.AddRange(step.Uses
                    .Select(action => action.Split('@')[0])
                    .Where(action => !ActionsThatGateNothingLocally.ContainsKey(action))
                    .Select(action => $"{step.Where}: uses {action}"));

                continue;
            }

            var gates = GatesOf(step);
            if (gates.Length == 0)
            {
                unaccounted.Add($"{step.Where}: runs something this rule does not read as a gate");
                continue;
            }

            var missing = gates.Except(local).ToArray();
            if (missing.Length > 0)
            {
                unaccounted.AddRange(missing.Select(gate => $"{step.Where}: {gate}"));
                continue;
            }

            mirrored.Add(step);
        }

        return (mirrored, unaccounted);
    }

    /// <summary>
    /// The local gates run every gate the workflows run that belongs on this machine.
    /// </summary>
    /// <remarks>
    /// The two local files can agree and both miss what CI runs, so every step of every job is
    /// accounted for: a local gate, a listed action, or a recorded reason.
    /// </remarks>
    [Fact]
    public void RunEveryGateTheWorkflowRunsThatBelongsOnThisMachine()
    {
        var steps = Workflows.SelectMany(StepsOf).ToList();
        steps.Should().NotBeEmpty("the workflows are the source of the list");

        var local = Identities(GateCommands(Path.Combine(RepositoryLayout.Root, "scripts", "gates.sh")));
        local.Should().NotBeEmpty("scripts/gates.sh is what has to hold it");

        var (mirrored, unaccounted) = Account(steps, local);

        Named(unaccounted).Should().BeEmpty(
            "each of these runs in CI and nowhere else, so a push is the first thing that " +
            "finds out - add it to scripts/gates.sh and to scripts/hooks/pre-push, or name it " +
            "in StepsLeftToContinuousIntegration (a step) or ActionsThatGateNothingLocally " +
            "(an action) with the reason it stays there");

        Named(local.Except(mirrored.SelectMany(GatesOf)).Except(LocalOnlyGates)).Should().BeEmpty(
            "the local gates run this and no workflow does, so it is enforced on the one " +
            "machine that already passed it and on nothing that merges - add it to " +
            ".github/workflows/dotnet.yml, or list it in LocalOnlyGates with the reason");

        var names = steps.Select(step => step.Name).ToHashSet(StringComparer.Ordinal);
        Named(StepsLeftToContinuousIntegration.Keys.Where(name => !names.Contains(name)))
            .Should().BeEmpty(
                "a row for a step no workflow declares any more exempts nothing, and it reads " +
                "as a decision about the pipeline as it stands");

        var actions = steps
            .SelectMany(step => step.Uses)
            .Select(action => action.Split('@')[0])
            .ToHashSet(StringComparer.Ordinal);
        Named(ActionsThatGateNothingLocally.Keys.Where(action => !actions.Contains(action)))
            .Should().BeEmpty("the same, for an action nothing uses any longer");

        // The header table of the hook, one row per job: "-" has to mean nothing of the job runs here.
        var declared = File.ReadAllLines(Path.Combine(RepositoryLayout.Root, "scripts", "hooks", "pre-push"))
            .Select(line => Regex.Match(line, @"^#\s+([a-z][a-z0-9-]*)\s+([+-])\s"))
            .Where(row => row.Success)
            .ToDictionary(row => row.Groups[1].Value, row => row.Groups[2].Value, StringComparer.Ordinal);

        declared.Should().NotBeEmpty("the header of the hook is the table this compares against");

        var contributing = mirrored.Select(step => step.Job).ToHashSet(StringComparer.Ordinal);

        Named(declared
                .Where(row => (row.Value == "+") != contributing.Contains(row.Key))
                .Select(row => $"{row.Key} is marked \"{row.Value}\""))
            .Should().BeEmpty(
                "\"+\" means the hook runs gates of that job and \"-\" means it runs none of " +
                "them, and these rows say the opposite of what scripts/gates.sh does");
    }
}
