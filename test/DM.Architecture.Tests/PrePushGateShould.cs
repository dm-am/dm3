using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The hook that runs before a push is a claim about CI, and the claim has to hold.
/// </summary>
/// <remarks>
/// It arrived saying it "runs exactly what CI runs". It did not: the solution was
/// built in Release and then tested in Debug, which compiled everything a second
/// time and ran the tests against binaries the build gate had never seen, and
/// dependency-scan was absent altogether — the job both publish jobs name in
/// needs, and the one that twice found vulnerable packages only after they had
/// reached origin.
///
/// So parity is asserted rather than trusted, and asserted against the workflow
/// rather than against a copy of it: the configuration is read out of the build
/// job, the commands are read out of dependency-scan, and every job of both
/// workflows has to appear in the header of the hook either as run locally or as
/// deliberately left to CI with a reason. A job added to CI turns this red until
/// someone decides which of the two it is.
///
/// Cost is part of that decision rather than a footnote: a gate that takes twenty
/// minutes gets bypassed with --no-verify, and a bypassed gate protects nobody.
/// Which is why what belongs to CI stays in CI, and says why.
/// </remarks>
public class PrePushGateShould
{
    private const string NpmAudit = "npm audit";

    /// <summary>
    /// A row of the coverage table in the hook header: the job, "+" or "-", and the
    /// note. The note is required by both markers: "-" without one is exactly the
    /// silence this class exists against, and "+" without one names no command.
    /// </summary>
    private static readonly Regex JobDecision = new(
        @"^#\s+([a-z][a-z0-9-]*)\s+([+-])\s+(\S.*)$", RegexOptions.Compiled);

    /// <summary>A job: below jobs: it is the only key written at two spaces.</summary>
    private static readonly Regex JobName = new(
        @"^  ([a-z][a-z0-9-]*):\s*$", RegexOptions.Compiled);

    /// <summary>The value of a run: step, in the plain form and in the block one.</summary>
    private static readonly Regex RunStep = new(
        @"^\s*(?:- )?run:\s*(.+)$", RegexOptions.Compiled);

    /// <summary>The lookbehind keeps --collect: and its like out of the match.</summary>
    private static readonly Regex Configuration = new(
        @"(?<![\w-])-c\s+(\w+)", RegexOptions.Compiled);

    private static readonly Regex ScriptPath = new(@"scripts/[\w.-]+\.sh", RegexOptions.Compiled);

    private static readonly Regex Flag = new(@"--[\w-]+(?:=[\w.]+)?", RegexOptions.Compiled);

    private static readonly string[] Workflows = ["dotnet.yml", "security.yml"];

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string HookPath => Path.Combine(RepositoryRoot, "scripts", "hooks", "pre-push");

    private static string WorkflowPath(string workflow) =>
        Path.Combine(RepositoryRoot, ".github", "workflows", workflow);

    /// <summary>
    /// The commands of the hook. Prose that names a command is not a command, and
    /// the header names several of them.
    /// </summary>
    private static List<string> HookCommands()
    {
        File.Exists(HookPath).Should().BeTrue($"the hook must exist at {HookPath}");

        return File.ReadAllLines(HookPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();
    }

    /// <summary>The lines of one job, from its key to the next job's, comments out.</summary>
    private static List<string> JobBody(string workflow, string job)
    {
        var path = WorkflowPath(workflow);
        File.Exists(path).Should().BeTrue($"the workflow must exist at {path}");

        var lines = File.ReadAllLines(path);
        var start = Array.FindIndex(
            lines, line => line.StartsWith($"  {job}:", StringComparison.Ordinal));
        start.Should().BeGreaterThan(-1, $"{workflow} must still declare the {job} job");

        var end = start + 1;
        while (end < lines.Length && !JobName.IsMatch(lines[end]))
        {
            end++;
        }

        return lines[start..end]
            .Where(line => !line.TrimStart().StartsWith('#'))
            .ToList();
    }

    /// <summary>The commands of one job: the value of every run: step it has.</summary>
    private static List<string> CiCommands(string workflow, string job)
    {
        var commands = new List<string>();
        var blockIndent = -1;

        foreach (var line in JobBody(workflow, job))
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }

            var indent = line.Length - line.TrimStart().Length;
            if (blockIndent >= 0 && indent > blockIndent)
            {
                commands.Add(line.Trim());
                continue;
            }

            blockIndent = -1;
            var match = RunStep.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var value = match.Groups[1].Value.Trim();
            if (value.StartsWith('|') || value.StartsWith('>'))
            {
                blockIndent = indent;
            }
            else
            {
                commands.Add(value);
            }
        }

        return commands;
    }

    private static IEnumerable<string> Jobs(string workflow)
    {
        var lines = File.ReadAllLines(WorkflowPath(workflow));
        var start = Array.FindIndex(lines, line => line.StartsWith("jobs:", StringComparison.Ordinal));
        start.Should().BeGreaterThan(-1, $"{workflow} must declare jobs");

        return lines
            .Skip(start + 1)
            .Select(line => JobName.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value);
    }

    private static string ConfigurationOf(IEnumerable<string> commands, string command, string source)
    {
        var line = commands.FirstOrDefault(c => c.Contains(command, StringComparison.Ordinal));
        line.Should().NotBeNull($"{source} must run {command}");

        var match = Configuration.Match(line!);
        match.Success.Should().BeTrue($"{source} must name a configuration for {command}: {line}");
        return match.Groups[1].Value;
    }

    /// <summary>
    /// The configuration is read out of the workflow rather than written down here:
    /// what is asserted is that the two agree, not which one they agree on.
    /// </summary>
    [Fact]
    public void BuildAndTestInTheConfigurationCiShips()
    {
        var ci = CiCommands("dotnet.yml", "build");
        var hook = HookCommands();

        ConfigurationOf(hook, "dotnet build", "the hook").Should().Be(
            ConfigurationOf(ci, "dotnet build", "the build job"),
            "the image publishes what Release compiles, so Release is what a gate has to compile");

        ConfigurationOf(hook, "dotnet test", "the hook").Should().Be(
            ConfigurationOf(ci, "dotnet test", "the build job"),
            "a test sensitive to the build configuration is not run locally at all while the " +
            "hook tests a configuration CI never tests");

        hook.Should().Contain(
            line => line.Contains("dotnet test", StringComparison.Ordinal)
                    && line.Contains("--no-build", StringComparison.Ordinal),
            "the line above has already built the solution: without --no-build the hook compiles " +
            "it a second time and checks binaries the build gate never saw");
    }

    /// <summary>
    /// dependency-scan stands in the needs of both publish jobs, so red there stops
    /// the release entirely, and it is the one job whose failures have twice been
    /// discovered after the push rather than before it.
    /// </summary>
    [Fact]
    public void RunEveryGateOfTheDependencyScanJob()
    {
        var ci = CiCommands("dotnet.yml", "dependency-scan");
        var hook = HookCommands();

        var scripts = ci
            .SelectMany(command => ScriptPath.Matches(command).Select(match => match.Value))
            .Distinct()
            .ToList();

        scripts.Should().NotBeEmpty("the job checks .NET advisories through a script");
        foreach (var script in scripts)
        {
            hook.Should().Contain(line => line.Contains(script, StringComparison.Ordinal),
                $"{script} gates both publish jobs, and a gate is cheap locally and expensive in CI");
        }

        var audited = ci.FirstOrDefault(command => command.Contains(NpmAudit, StringComparison.Ordinal));
        audited.Should().NotBeNull("the job audits the packages that reach a browser");

        var invocation = audited!;
        var flags = Flag
            .Matches(invocation[invocation.IndexOf(NpmAudit, StringComparison.Ordinal)..])
            .Select(match => match.Value)
            .ToList();
        flags.Should().NotBeEmpty("an audit with no flags reports rather than gates");

        var locally = hook.FirstOrDefault(line => line.Contains(NpmAudit, StringComparison.Ordinal));
        locally.Should().NotBeNull("the hook has to audit them too");
        foreach (var flag in flags)
        {
            locally!.Should().Contain(flag,
                "--audit-level decides the exit code and --omit decides the scope, so an audit " +
                "run without them answers a different question than CI asks");
        }
    }

    /// <summary>
    /// A job marked "+" runs the gates of that job, not a subset of them.
    /// </summary>
    /// <remarks>
    /// The header claimed the frontend job while the hook ran "npx vitest run"
    /// against a job that runs "npm run test:coverage" — the same tests, and not
    /// the same gate: the thresholds live in coverage.thresholds of vite.config.ts
    /// and apply only under --coverage, so deleting a suite passed locally and
    /// failed in CI. AccountForEveryJobOfBothWorkflows compares job names only, so
    /// a "+" over a subset was invisible to it.
    ///
    /// Read as scripts rather than as command lines: package.json is where a
    /// frontend gate is defined, and both sides address it by the same name.
    /// </remarks>
    [Fact]
    public void RunEveryScriptOfTheFrontendJob()
    {
        var scriptCall = new Regex(@"npm run ([\w:-]+)", RegexOptions.Compiled);

        var ci = CiCommands("dotnet.yml", "frontend")
            .SelectMany(command => scriptCall.Matches(command).Select(match => match.Groups[1].Value))
            .Distinct()
            .ToList();

        ci.Should().NotBeEmpty("the frontend job runs its gates through npm scripts");

        var hook = HookCommands();
        foreach (var script in ci)
        {
            hook.Should().Contain(line => line.Contains($"npm run {script}", StringComparison.Ordinal),
                $"the header marks frontend as run locally, and {script} is one of its gates; " +
                "running an equivalent-looking command instead answers a different question");
        }
    }

    /// <summary>
    /// Every job is either run by the hook or named in its header as left to CI.
    /// The third option, saying nothing, is the one that produced a local gate green
    /// on a push CI then failed.
    /// </summary>
    [Fact]
    public void AccountForEveryJobOfBothWorkflows()
    {
        var declared = Workflows.SelectMany(Jobs).ToList();
        declared.Should().NotBeEmpty("the parser must find the jobs");

        var listed = File.ReadAllLines(HookPath)
            .Select(line => JobDecision.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .ToList();

        listed.Should().BeEquivalentTo(declared,
            "the header marks every job as run (+) or as left to CI (-) with the reason; a job " +
            "missing from the table is the silent gap this hook shipped with, and a row naming a " +
            "job that no longer exists is a promise about nothing");
    }

    /// <summary>
    /// Every hook has a test, and every one of those tests is run before a push.
    /// </summary>
    /// <remarks>
    /// The hooks are the last thing standing between a forbidden command and lost
    /// work, and they are the one part of the tree no workflow covers: CI never
    /// invokes them, so nothing outside this hook would notice a rule that stopped
    /// matching. One of the two shipped with a test on thirty-five cases that
    /// nothing ever ran.
    ///
    /// Both halves matter. A hook with no test is unverified; a test nothing calls
    /// is a file that looks like a gate and is not one.
    /// </remarks>
    [Fact]
    public void RunTheTestOfEveryHook()
    {
        var hooks = Directory
            .GetFiles(Path.Combine(RepositoryRoot, ".claude", "hooks"), "*.js")
            .Select(Path.GetFileName)
            .ToList();

        var rules = hooks!.Where(name => !name!.EndsWith(".test.js", StringComparison.Ordinal)).ToList();
        rules.Should().NotBeEmpty("the hooks directory is what this rule is about");

        var hook = File.ReadAllText(HookPath);
        var unverified = new List<string>();
        var unrun = new List<string>();

        foreach (var rule in rules)
        {
            var test = Path.GetFileNameWithoutExtension(rule) + ".test.js";
            if (!hooks.Contains(test, StringComparer.Ordinal))
            {
                unverified.Add(rule!);
                continue;
            }

            if (!hook.Contains(test, StringComparison.Ordinal))
            {
                unrun.Add(test);
            }
        }

        unverified.Should().BeEmpty(
            "a hook nothing exercises is checked only by the work it fails to stop");
        unrun.Should().BeEmpty(
            "the test exists and nothing calls it, which is the state the whole rule is against");
    }
}
