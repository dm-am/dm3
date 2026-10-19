using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A host that borrows the static logger has to give it back.
/// </summary>
/// <remarks>
/// UseSerilog() takes Log.Logger with dispose: false, so the Loki sink dies with its last batch.
/// Source text: the call lives in Main, which no test runs.
/// </remarks>
public class LogFlushOnShutdownShould
{
    private static string SourceDirectory => Path.Combine(DM.Testing.RepositoryLayout.Root, "src");

    [Fact]
    public void FlushInEveryHostThatSetsSerilogAsTheProvider()
    {
        var entryPoints = Directory
            .EnumerateDirectories(SourceDirectory)
            .Select(project => Path.Combine(project, "Program.cs"))
            .Where(File.Exists)
            .Where(entryPoint => File.ReadAllText(entryPoint).Contains("UseSerilog(", StringComparison.Ordinal))
            .ToList();

        entryPoints.Should().NotBeEmpty("the walk must find the hosts that configure Serilog");
        foreach (var entryPoint in entryPoints)
        {
            File.ReadAllText(entryPoint).Should().Contain("Log.CloseAndFlush()",
                $"{entryPoint} borrows the static logger, and dispose: false means nobody else returns it");
        }
    }

    [Fact]
    public void FlushBeforeTheMigrationModeKillsTheProcess()
    {
        var startup = File.ReadAllText(Path.Combine(SourceDirectory, "DM.Web.API", "Startup.cs"));

        var exit = startup.IndexOf("Environment.Exit(", StringComparison.Ordinal);
        var flush = startup.IndexOf("Log.CloseAndFlush()", StringComparison.Ordinal);

        exit.Should().BeGreaterThan(-1, "migration mode still ends the process by itself");
        flush.Should().BeGreaterThan(-1,
            "Environment.Exit runs no finally block, so the flush in Main never happens on this path");
        flush.Should().BeLessThan(exit, "a flush after the process is gone flushes nothing");
    }

    /// <summary>
    /// The runtime is PID 1 in the container, so it hears the stop signal at all.
    /// </summary>
    /// <remarks>The entry point runs through a shell to interpolate the project name; without exec the shell stays PID 1.</remarks>
    [Fact]
    public void HandTheStopSignalToTheRuntimeItself()
    {
        var dockerfile = File.ReadAllText(Path.Combine(
            DM.Testing.RepositoryLayout.Root, "docker", "app.Dockerfile"));

        var entryPoint = dockerfile
            .Split('\n')
            .LastOrDefault(line => line.TrimStart().StartsWith("ENTRYPOINT", StringComparison.Ordinal));

        entryPoint.Should().NotBeNull("the image has to start something");
        entryPoint.Should().Contain("exec ",
            "without it the shell keeps PID 1 and the runtime is never told to stop");
    }
}
