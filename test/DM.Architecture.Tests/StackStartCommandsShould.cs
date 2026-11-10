using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <remarks>
/// Compose refuses an up that names a service no file declares, and it does so on the runner or
/// the server, after everything before that line has run. The workflows and the scripts are read
/// here, so a service that leaves the stack leaves every command that starts or waits for it.
/// </remarks>
public class StackStartCommandsShould
{
    private static readonly Regex ShellUp = new(@"compose\b[^\n]*?\bup\b(?<rest>[^\n]*)", RegexOptions.Compiled);
    private static readonly Regex ArrayUp = new(@"""compose"",\s*""up"",(?<rest>[^\]]*)\]", RegexOptions.Compiled);
    private static readonly Regex Token = new(@"^(-[-\w=]+|[a-z][a-z0-9-]*)$", RegexOptions.Compiled);
    private static readonly Regex WaitedContainer = new(@"Name = ""(?<name>dm-[a-z0-9-]+)""", RegexOptions.Compiled);

    /// <remarks>Mutation it must fail on: a service removed from the compose file and kept in a start command.</remarks>
    [Fact]
    public void StartOnlyServicesTheStackDeclares()
    {
        var services = DeclaredServices();
        services.Should().Contain(["postgres", "dmapi", "migration"],
            "the services are read off the compose files, and a walk that finds none checks nothing");

        var started = Commands()
            .SelectMany(command => StartedServices(command.Text).Select(service => (command.File, Service: service)))
            .ToList();
        started.Should().Contain(entry => entry.Service == "postgres",
            "the workflows and the scripts start the database, and a walk that finds no start checks nothing");

        started.Where(entry => !services.Contains(entry.Service))
            .Select(entry => $"{entry.File}: {entry.Service}")
            .Should().BeEmpty("compose refuses to start a service no file declares");
    }

    /// <remarks>Mutation it must fail on: a container removed from the stack and still awaited by the start command.</remarks>
    [Fact]
    public void WaitOnlyForContainersTheStackRuns()
    {
        var script = RepositoryFiles.Read("scripts/dm/Services.ps1");
        var waited = WaitedContainer.Matches(script).Select(match => match.Groups["name"].Value).ToList();
        waited.Should().Contain("dm-pg", "the start command waits for the database, and a walk that finds none checks nothing");

        var compose = Read(BaseCompose);
        waited.Where(name => !compose.Contains($"container_name: '{name}'", StringComparison.Ordinal))
            .Should().BeEmpty("a container the stack does not run never turns healthy, so the start waits out its timeout and fails");
    }

    private static HashSet<string> DeclaredServices() => Directory
        .EnumerateFiles(DockerDirectory, "docker-compose*.yml")
        .Select(File.ReadAllText)
        .Select(text => Regex.Match(text, @"(?ms)^services:\s*\n(?<body>.*?)(?=^\S|\z)").Groups["body"].Value)
        .SelectMany(body => Regex.Matches(body, @"(?m)^  (?<name>[a-z][a-z0-9-]*):").Select(match => match.Groups["name"].Value))
        .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<(string File, string Text)> Commands()
    {
        var root = RepositoryLayout.Root;
        var files = Directory.EnumerateFiles(Path.Combine(root, ".github", "workflows"), "*.yml")
            .Concat(new[] { "*.sh", "*.ps1", "*.mjs" }
                .SelectMany(pattern => Directory.EnumerateFiles(Path.Combine(root, "scripts"), pattern, SearchOption.AllDirectories)))
            .Concat(Directory.EnumerateFiles(DockerDirectory, "*.sh", SearchOption.AllDirectories))
            .Where(RepositoryFiles.IsAuthored);

        return files.Select(path => (RepositoryFiles.Relative(path), File.ReadAllText(path)));
    }

    private static IEnumerable<string> StartedServices(string text)
    {
        var shell = ShellUp.Matches(text)
            .Select(match => match.Groups["rest"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var array = ArrayUp.Matches(text)
            .Select(match => match.Groups["rest"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim().Trim('"')).ToArray());

        return shell.Concat(array)
            .SelectMany(tokens => tokens.TakeWhile(token => Token.IsMatch(token)))
            .Where(token => !token.StartsWith('-'));
    }
}
