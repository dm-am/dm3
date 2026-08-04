using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A host that borrows the static logger has to give it back.
/// </summary>
/// <remarks>
/// UseSerilog() with no argument takes Log.Logger with dispose: false, so nothing
/// disposes it when the host shuts down and the Loki sink — which ships on a timer
/// — dies with its last batch still buffered. The lost batch is always the same
/// one: the lines that say why the process stopped. Nothing fails and nothing
/// warns, so the absence has to be asserted rather than observed.
///
/// Source text is the only surface there is: the call lives in Main, which no test
/// can run, and leaving it out changes nothing observable about the built host.
/// </remarks>
public class LogFlushOnShutdownShould
{
    /// <summary>
    /// Walks up from the test binary to the repository root. Entry points are not
    /// copied to the output directory, and copying them would let this assert
    /// against a stale snapshot.
    /// </summary>
    private static string SourceDirectory
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return Path.Combine(directory!.FullName, "src");
        }
    }

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
}
