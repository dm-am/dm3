using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Parity is asserted against the workflow: the build configuration, the dependency-scan commands,
/// and every job named in the hook header as run locally or left to CI with a reason. Which part a
/// branch push runs is AgentToolingShould's.
/// </remarks>
public class PrePushGateShould
{
    private const string NpmAudit = "npm audit";

    private static readonly Regex JobDecision = new(
        @"^#\s+([a-z][a-z0-9-]*)\s+([+-])\s+(\S.*)$", RegexOptions.Compiled);

    private static readonly Regex RunStep = new(
        @"^\s*(?:- )?run:\s*(.+)$", RegexOptions.Compiled);

    // The lookbehind keeps --collect: and its like out of the match.
    private static readonly Regex Configuration = new(
        @"(?<![\w-])-c\s+(\w+)", RegexOptions.Compiled);

    private static readonly Regex ScriptPath = new(@"scripts/[\w.-]+\.sh", RegexOptions.Compiled);

    private static readonly Regex Flag = new(@"--[\w-]+(?:=[\w.]+)?", RegexOptions.Compiled);

    private static readonly Regex GateInvocation = new(
        @"^(?:node|bash|npm|dotnet)\s|^\(cd\s|=\$\(node\s", RegexOptions.Compiled);

    /// <summary>The branches pull requests are opened against, as the trigger of the workflow lists them.</summary>
    private static readonly Regex PullRequestTargets = new(
        @"^  pull_request:\s+branches:\s*\[([^\]]*)\]", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly string[] Workflows = ["dotnet.yml", "security.yml"];

    private static string HookPath => Path.Combine(RepositoryLayout.Root, "scripts", "hooks", "pre-push");

    // Prose that names a command is not a command, and the header names several of them.
    private static List<string> HookCommands()
    {
        File.Exists(HookPath).Should().BeTrue($"the hook must exist at {HookPath}");

        return File.ReadAllLines(HookPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();
    }

    private static List<string> JobBody(string workflow, string job) =>
        WorkflowJobs.Body(workflow, job).Where(WorkflowJobs.IsCode).ToList();

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

    private static IEnumerable<string> Jobs(string workflow) => WorkflowJobs.Read(workflow).Select(job => job.Name);

    private static string ConfigurationOf(IEnumerable<string> commands, string command, string source)
    {
        var line = commands.FirstOrDefault(c => c.Contains(command, StringComparison.Ordinal));
        line.Should().NotBeNull($"{source} must run {command}");

        var match = Configuration.Match(line!);
        match.Success.Should().BeTrue($"{source} must name a configuration for {command}: {line}");
        return match.Groups[1].Value;
    }

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
    /// dependency-scan stands in the needs of both publish jobs.
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

    [Fact]
    public void RefuseAPushToABranchThatTakesOnlyMerges()
    {
        var trigger = PullRequestTargets.Match(File.ReadAllText(WorkflowJobs.PathOf("dotnet.yml")));
        trigger.Success.Should().BeTrue("dotnet.yml runs on the pull requests into the branches that take them");

        var branches = trigger.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        branches.Should().Contain("dev", "dev is where every change lands");

        var hook = HookCommands();
        var refs = hook.FindIndex(line => line.StartsWith("while read", StringComparison.Ordinal));
        var firstGate = hook.FindIndex(GateInvocation.IsMatch);

        refs.Should().BeGreaterThan(-1, "git hands the hook the refs being pushed on stdin, one per line");
        firstGate.Should().BeGreaterThan(-1, "the hook runs gates");

        foreach (var branch in branches)
        {
            var arm = hook.FindIndex(line => line.EndsWith(')')
                && line[..^1].Split('|', StringSplitOptions.TrimEntries).Contains($"refs/heads/{branch}"));

            arm.Should().BeGreaterThan(refs, $"a push to {branch} is told apart by the remote ref the hook reads");
            arm.Should().BeLessThan(firstGate, $"a push to {branch} is refused before the gates spend their minutes on it");

            var body = hook.Skip(arm + 1).TakeWhile(line => line != ";;").ToList();
            body.Should().Contain(line => line.Contains("принимает только слияния PR", StringComparison.Ordinal),
                "the refusal names the way a change does reach the branch");
            body.Should().Contain(line => Regex.IsMatch(line, @"^exit [1-9]"),
                "a refusal that exits with 0 lets the push through");
        }
    }

    // CI never invokes the hooks; a test nothing calls looks like a gate and is not one.
    [Fact]
    public void RunTheTestOfEveryHook()
    {
        var hooks = Directory
            .GetFiles(Path.Combine(RepositoryLayout.Root, ".claude", "hooks"), "*.js")
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
