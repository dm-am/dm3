using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Configuration answers a question once.
/// </summary>
/// <remarks>
/// Two ways of breaking that, both silent. The host decides what environment a
/// process runs in - it reads DOTNET_ and ASPNETCORE_ variables and the command
/// line - and the logging setup read the variable itself, so `--environment
/// Development` gave a pipeline that mounts Swagger and a log that labels its
/// stream Production. And two configuration sources declared reloadOnChange while
/// nothing anywhere reads IOptionsMonitor or IOptionsSnapshot, which bought a file
/// watcher and the promise that editing the secrets file applies.
///
/// Asserted on the text: both live in composition, which no test executes, and
/// neither changes anything observable about the built host.
/// </remarks>
public class ConfigurationSourceOfTruthShould
{
    /// <summary>Spelled in halves so this file does not match its own rule.</summary>
    private static readonly string EnvironmentVariable = "ASPNETCORE" + "_ENVIRONMENT";

    [Fact]
    public void FindTheSourcesItReads() =>
        Sources().Should().HaveCountGreaterThan(200,
            "a walk that matches nothing passes");

    [Fact]
    public void LetTheHostDecideWhatEnvironmentThisIs() =>
        Sources()
            .Where(path => File.ReadAllText(path).Contains(EnvironmentVariable, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .Should().BeEmpty(
                "the variable is one of several inputs the host folds into IHostEnvironment, " +
                "and reading it directly produces a second, disagreeing answer inside the " +
                "same process");

    [Fact]
    public void DeclareNoReloadNobodyReads()
    {
        var readsUpdates = Sources().Any(path =>
        {
            var text = File.ReadAllText(path);
            return text.Contains("IOptionsMonitor", StringComparison.Ordinal) ||
                   text.Contains("IOptionsSnapshot", StringComparison.Ordinal);
        });

        if (readsUpdates)
        {
            return;
        }

        Sources()
            .Where(path => File.ReadAllText(path).Contains("reloadOnChange: true", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .Should().BeEmpty(
                "every consumer holds the snapshot taken at first resolution, so the flag " +
                "only promises an update that never arrives and never gets validated");
    }

    private static string[] Sources() => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(path => !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin" or "node_modules"))
        .ToArray();

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
