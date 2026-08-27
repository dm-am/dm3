using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Nothing an agent is handed starts a server on the port the owner has open.
/// </summary>
/// <remarks>
/// The owner's dev server is pinned with strictPort, so there are two outcomes.
/// Either the agent's server refuses to start on a busy port, or it takes the
/// port while the owner's is down and the tab he has open is served by a
/// different build without saying so. The second one is the expensive one:
/// whatever is measured there gets reported as a fact about his stand.
///
/// The trap has been closed twice already and came back both times, once as a
/// second launch configuration and once as a default base URL for the e2e runner.
/// Both were found by reading, which is why no port is written here. It is read
/// out of the config that pins it, and the npm scripts that start that server are
/// read out of package.json, so a renamed script or a moved port keeps the rule
/// pointed at the right thing.
///
/// Only fenced blocks are searched. What gets pasted into a shell is what sits in
/// a fence, and a brief has to stay able to name in prose the command it forbids.
/// </remarks>
public class AgentToolingShould
{
    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string ClientPath(string file) =>
        Path.Combine(RepositoryRoot, "src", "DM.Web.Client", file);

    /// <summary>The port the owner's server pins, read out of the config that pins it.</summary>
    private static string OwnerPort()
    {
        var declaration = Regex.Match(
            File.ReadAllText(ClientPath("vite.config.ts")),
            @"server:\s*\{\s*port:\s*(\d+)");

        declaration.Success.Should().BeTrue("the dev server declares its port in vite.config.ts");
        return declaration.Groups[1].Value;
    }

    /// <summary>
    /// The npm scripts that start that server. "vite build" and "vite preview" are
    /// other programs sharing the executable, and a script pointed at another
    /// config is pointed at another port.
    /// </summary>
    private static bool StartsTheDevServer(string command)
    {
        var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0
               && words[0] == "vite"
               && (words.Length == 1 || words[1].StartsWith('-'))
               && !command.Contains("--config", StringComparison.Ordinal);
    }

    private static string[] DevServerScripts()
    {
        using var package = JsonDocument.Parse(File.ReadAllText(ClientPath("package.json")));

        return package.RootElement.GetProperty("scripts").EnumerateObject()
            .Where(script => StartsTheDevServer(script.Value.GetString() ?? string.Empty))
            .Select(script => script.Name)
            .ToArray();
    }

    private static string[] Briefs() =>
        Directory.GetFiles(Path.Combine(RepositoryRoot, ".claude", "agents"), "*.md");

    /// <summary>The lines a brief hands over to be run, as opposed to the prose about them.</summary>
    private static IEnumerable<(string Brief, string Line)> FencedLines(string path)
    {
        var brief = new FileInfo(path).Name;
        var inside = false;

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inside = !inside;
            }
            else if (inside)
            {
                yield return (brief, line);
            }
        }
    }

    [Fact]
    public void HandOutNoCommandThatBindsTheOwnersPort()
    {
        var port = OwnerPort();
        var scripts = DevServerScripts();
        var briefs = Briefs();

        scripts.Should().NotBeEmpty("the rule below is written in terms of those scripts");
        briefs.Should().NotBeEmpty("the briefs are what this reads");

        var offenders = briefs
            .SelectMany(FencedLines)
            .Where(command =>
                command.Line.Contains(port, StringComparison.Ordinal) ||
                scripts.Any(script =>
                    command.Line.Contains($"npm run {script}", StringComparison.Ordinal)))
            .Select(command => $"{command.Brief}: {command.Line.Trim()}")
            .OrderBy(offender => offender, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "a subagent runs what its brief hands it, and this binds the port the " +
            "owner's browser is pointed at, see the class remarks");
    }

    /// <summary>
    /// Commands that destroy the owner's data, spelled however the project spells
    /// them.
    /// </summary>
    /// <remarks>
    /// The port rule above and this one come from the same fact — a subagent runs
    /// what its brief hands it — and only one of them was written down. The
    /// debugger's brief listed `dm.ps1 reset` under the heading "Restart
    /// services", and that command is `compose down -v`: every account, game and
    /// post on the stand, gone, because a diagnosis wanted a fresh log.
    ///
    /// Matched on the wrapper as well as on the underlying compose flag. Checking
    /// only for `-v` would pass the wrapper, which is the spelling a brief
    /// actually uses, and checking only for the wrapper would pass the raw
    /// command a brief might grow later.
    /// </remarks>
    private static readonly string[] DestroysTheStand =
    {
        "dm.ps1 reset",
        "down -v",
        "down --volumes",
        "volume rm",
        "volume prune",
        "system prune",
    };

    [Fact]
    public void HandOutNoCommandThatWipesTheOwnersData()
    {
        var briefs = Briefs();
        briefs.Should().NotBeEmpty("the briefs are what this reads");

        var offenders = briefs
            .SelectMany(FencedLines)
            .Where(command => DestroysTheStand.Any(destructive =>
                command.Line.Contains(destructive, StringComparison.OrdinalIgnoreCase)))
            .Select(command => $"{command.Brief}: {command.Line.Trim()}")
            .OrderBy(offender => offender, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "a subagent runs what its brief hands it, and these empty the databases " +
            "the owner's stand is running on");
    }

    [Fact]
    public void RecogniseADestructiveCommandWhenItSeesOne()
    {
        // The rule is a substring scan, and a scan that stops matching passes in
        // silence.
        DestroysTheStand.Should().Contain(destructive =>
            ".\\scripts\\dm.ps1 reset".Contains(destructive, StringComparison.OrdinalIgnoreCase),
            "this is the line the rule exists for");
        DestroysTheStand.Should().Contain(destructive =>
            "docker compose down -v --remove-orphans".Contains(destructive, StringComparison.OrdinalIgnoreCase));
        DestroysTheStand.Should().NotContain(destructive =>
            ".\\scripts\\dm.ps1 status".Contains(destructive, StringComparison.OrdinalIgnoreCase),
            "reading the state of the stand is what these briefs are for");
    }

    [Fact]
    public void PreviewOnAPortOfItsOwn()
    {
        using var launch = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot, ".claude", "launch.json")));

        var declared = launch.RootElement.GetProperty("configurations").EnumerateArray()
            .Where(configuration => configuration.TryGetProperty("port", out _))
            .Select(configuration => configuration.GetProperty("port").GetInt32())
            .ToArray();

        var port = OwnerPort();

        declared.Should().NotBeEmpty("a preview configuration names the port it binds");
        declared.Should().NotContain(preview => $"{preview}" == port,
            "a preview started from here would serve the owner's open tab out of the " +
            "assistant's build, and the browser shows no difference");
    }

    /// <summary>
    /// How a gate looks written down, in a shell script or in a workflow step.
    /// </summary>
    /// <remarks>
    /// Digits belong in an npm script name: without them "npm run test:e2e:fast"
    /// is read as "npm run test:e", and a rule that reports the wrong name sends
    /// whoever hits it looking for a gate that does not exist.
    /// </remarks>
    /// <remarks>
    /// `npm ci` takes its flags and stops, where the rest run to the end of the
    /// line: dependency-scan writes `npm ci &amp;&amp; npm audit ...` in one step, and a
    /// greedy match there would swallow the audit and report a job that does not
    /// audit anything.
    /// </remarks>
    private static readonly Regex GateSyntax = new(
        @"(npm run [a-z0-9:-]+|npm ci\b(?: --[a-z-]+)*|npm audit [^\r\n)]+|dotnet (?:format|build|test)[^\r\n)]*|check-vulnerable-packages\.sh|check-coverage\.sh|check-shell-scripts\.sh|block-dangerous-git\.test\.js|block-new-migrations\.test\.js|lint-edited-file\.test\.js)",
        RegexOptions.Compiled);

    /// <summary>The gate commands of a shell script, in the order it runs them.</summary>
    /// <remarks>
    /// Read as commands rather than as lines: the hook writes them at the top
    /// level, the script hands them to a helper, so the same gate is spelled
    /// `(cd "$CLIENT" &amp;&amp; npm run lint:ci)` in one file and
    /// `in_client npm run lint:ci` in the other. What must match is which gates
    /// run, which is the tail of both spellings.
    /// </remarks>
    private static string[] GateCommands(string path)
    {
        // Comments are prose and name commands freely: both files warn in words
        // about running a second dotnet test alongside the gates.
        var code = string.Join("\n", File.ReadAllLines(path)
            .Where(line => !line.TrimStart().StartsWith("#")));

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
    /// <remarks>
    /// The list lives twice on purpose: the hook must protect a clone that has
    /// nothing else, and the script must run on an untouched tree. The cost of
    /// that duplication is drift, and drift here is silent in the worst
    /// direction: a gate added to the hook and not to the script turns the
    /// script into a promise it does not keep, and the next push spends twenty
    /// minutes discovering it.
    /// </remarks>
    [Fact]
    public void MirrorTheHookInTheGatesScript()
    {
        var hook = GateCommands(Path.Combine(RepositoryRoot, "scripts", "hooks", "pre-push"));
        var script = GateCommands(Path.Combine(RepositoryRoot, "scripts", "gates.sh"));

        hook.Should().NotBeEmpty("the hook is the source of the list");
        script.Should().BeEquivalentTo(hook,
            "scripts/gates.sh exists to answer before the push what the hook answers during it");
    }

    private static readonly string[] Workflows = { "dotnet.yml", "security.yml" };

    private static string WorkflowPath(string workflow) =>
        Path.Combine(RepositoryRoot, ".github", "workflows", workflow);

    /// <summary>
    /// Gates that exist locally and in no workflow, by design.
    /// </summary>
    /// <remarks>
    /// These test the local hooks themselves - the thing that stands between a
    /// forbidden git command and lost work. There is nothing for CI to protect
    /// there: by the time a push arrives the damage would already have happened
    /// on the machine that made it.
    /// </remarks>
    private static readonly string[] LocalOnlyGates =
    {
        "block-dangerous-git.test.js",
        "block-new-migrations.test.js",
        "lint-edited-file.test.js",
    };

    /// <summary>
    /// Actions whose use is never a gate on this machine, with the reason each.
    /// </summary>
    /// <remarks>
    /// A step that hands its work to an action does what the action does, so the
    /// decision belongs to the action and not to the step: the rows below stand
    /// for some thirty steps that check out a tree, install a toolchain, or move
    /// a file onto the run page. An action missing from the list fails the rule,
    /// which is the point - the next one added to CI is a decision somebody has
    /// to make out loud rather than a step that quietly runs nowhere else.
    /// </remarks>
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
    /// This register is the whole point of the rule that reads it. A step is
    /// either run before the push or written down here with the reason it is
    /// not; the third state - a check nobody outside CI has ever run - is what
    /// put three jobs in the red in one afternoon, and the one that hid longest
    /// was shellcheck, sitting inside a job whose single line of explanation
    /// priced it by its most expensive step.
    ///
    /// Rows are keyed by the name the workflow gives the step, so a name three
    /// jobs share is one decision. A step with no name is keyed by what it runs
    /// and wants a name before it lands here.
    ///
    /// Every key is held to the workflows below: a row naming a step that no
    /// longer exists is an exemption for nothing, and it reads as a decision
    /// about the pipeline as it stands.
    /// </remarks>
    private static readonly Dictionary<string, string> StepsLeftToContinuousIntegration =
        new(StringComparer.Ordinal)
        {
            // build
            ["Restore dependencies"] =
                "a local build restores as it goes; CI splits it out so that the build step " +
                "after it can pass --no-restore on a checkout that has no packages at all",

            // e2e - the suite itself, and the stack it needs standing
            ["Generate the throwaway secrets compose refuses to start without"] =
                "invents the values compose declares with ${...:?}; a machine here has " +
                "docker/.env with values of its own",
            ["Create .env for Docker Compose"] =
                "the same file, which on this machine exists and is not a copy of the example",
            ["Overlay that turns rate limiting off and lets the preview origin in"] =
                "writes the compose overlay the end-to-end job runs under",
            ["Start the stack"] =
                "brings the whole stack up under docker",
            ["Wait for API health"] =
                "waits on the stack the step before it started",
            ["Seed the database"] =
                "fills a database the suite reads; the stand here is seeded already",
            ["Install dependencies"] =
                "npm ci into an empty checkout; a clone has node_modules, and that the lock " +
                "file still describes them is the gate the local set runs instead",
            ["Install Chromium"] =
                "downloads a browser",
            ["Run the suite"] =
                "the end-to-end tier: a stack standing and twenty minutes, which is the price " +
                "at which a gate stops being run at all",
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

            // compose-topology - everything about deploying, which is the job's
            // subject; shellcheck is the one step that is not, and it runs locally
            ["Validate the deployed overlay combination"] =
                "deployment topology: three compose files and eleven deployment variables",
            ["Every build context the overlay names must exist"] =
                "deployment topology: reads the contexts out of a rendered overlay",
            ["The unit systemd runs must name the same files and profiles as the installer"] =
                "deployment topology: the systemd unit and the server installer",
            ["The edge and the SPA server configurations must parse"] =
                "deployment topology: four nginx containers and a generated certificate",
            ["The installer must refuse a directory it would install over"] =
                "deployment topology: the first act of the server installer",
            ["The installer of the environment file must carry a new variable over"] =
                "deployment topology: the environment file of a server, not of this machine",
            ["The Prometheus configuration and its alert rules must parse"] =
                "deployment topology: promtool inside the Prometheus image",
            ["The alert rules must fire on what they are written for"] =
                "deployment topology: the same image, replaying series through the rules",
            ["Every alert rule must reach a receiver the stack runs"] =
                "deployment topology: the alerting section against the deployed containers",
            ["The alert receiver must render its configuration and start"] =
                "deployment topology: starts alertmanager and waits for it to answer",

            // deployment-smoke
            ["Generate the stand credentials"] =
                "writes the htpasswd file the edge mounts on a server",
            ["Start the pair the installer and the unit deploy"] =
                "brings up the deployed pair, edge included",
            ["The edge answers where the deployment needs it to"] =
                "smoke test against the pair the step before it started",
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
    /// <remarks>
    /// Keyed on the first key of a step rather than on the dash alone: a run
    /// block can hold YAML of its own - the end-to-end job writes a compose
    /// overlay with a heredoc - and a list item in there is not a step.
    /// </remarks>
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
        var lines = File.ReadAllLines(WorkflowPath(workflow))
            .Where(line => !line.TrimStart().StartsWith("#", StringComparison.Ordinal));

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
    /// <remarks>
    /// Only the keys of the step itself are read. "name:" appears twice in an
    /// upload step - once as the title and once, inside with:, as the name of
    /// the artifact - and the second one is not what anybody calls that step.
    /// </remarks>
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
    /// The same gate is written differently in the two places on purpose: the
    /// workflow starts from an empty checkout and restores first, the local files
    /// run on a working tree and name the solution file. Holding the flags equal
    /// would make the rule fail on every legitimate difference and get switched
    /// off. What must match is which gates run.
    ///
    /// Collecting coverage is the exception, and it is not a flag: it is what
    /// writes the report the coverage gate reads, so a test run without it is a
    /// different gate - one that leaves the gate after it with nothing to measure.
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
            return command.Contains("--collect", StringComparison.Ordinal)
                ? "dotnet test --collect"
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
    /// The local gates run every gate the workflows run that belongs on this machine.
    /// </summary>
    /// <remarks>
    /// MirrorTheHookInTheGatesScript compares the two local files with each
    /// other, which is exactly the drift it cannot see: both of them can agree
    /// and both of them can be missing what CI runs. That is not hypothetical,
    /// and it has now cost two afternoons. The threshold on backend coverage
    /// lived in the workflow and in neither local file. shellcheck lived in
    /// compose-topology, a job this rule did not look at at all, because the
    /// rule used to read three jobs by name - so the class of defect it closed
    /// was "a gate of these three jobs", not "a gate of this pipeline".
    ///
    /// It reads every step of every job of both workflows now, and every step
    /// has to be accounted for: it runs gates this machine runs, or it hands its
    /// work to an action listed above, or it is named in the register above with
    /// the reason it stays in CI. A step that is none of those fails this rule
    /// rather than passing through it in silence, which is the only difference
    /// that matters.
    /// </remarks>
    [Fact]
    public void RunEveryGateTheWorkflowRunsThatBelongsOnThisMachine()
    {
        var steps = Workflows.SelectMany(StepsOf).ToList();
        steps.Should().NotBeEmpty("the workflows are the source of the list");

        var local = Identities(GateCommands(Path.Combine(RepositoryRoot, "scripts", "gates.sh")));
        local.Should().NotBeEmpty("scripts/gates.sh is what has to hold it");

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

        // Which jobs contribute something locally is also written down in the
        // header of the hook, one row per job, and that table is what a reader
        // consults. "-" has to mean none of it runs here: it said that about
        // compose-topology while shellcheck sat inside.
        var declared = File.ReadAllLines(Path.Combine(RepositoryRoot, "scripts", "hooks", "pre-push"))
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

    /// <summary>
    /// The offenders as one line, because an empty-collection failure prints the
    /// first item and these lists exist to name every place at once.
    /// </summary>
    private static string Named(IEnumerable<string> offenders) =>
        string.Join("; ", offenders.OrderBy(offender => offender, StringComparer.Ordinal));
}
