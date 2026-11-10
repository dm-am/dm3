using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// The host decides the environment, so nothing reads the variable itself; nothing reads
/// IOptionsMonitor, so no source reloads on change. Asserted on the text of composition.
/// </remarks>
public class ConfigurationSourceOfTruthShould
{
    // Spelled in halves so this file does not match its own rule.
    private static readonly string EnvironmentVariable = "ASPNETCORE" + "_ENVIRONMENT";

    [Fact]
    public void FindTheSourcesItReads() =>
        Sources().Should().NotBeEmpty("a walk that matches nothing passes");

    [Fact]
    public void LetTheHostDecideWhatEnvironmentThisIs() =>
        Sources()
            .Where(path => File.ReadAllText(path).Contains(EnvironmentVariable, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryLayout.Root, path))
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
            .Select(path => Path.GetRelativePath(RepositoryLayout.Root, path))
            .Should().BeEmpty(
                "every consumer holds the snapshot taken at first resolution, so the flag " +
                "only promises an update that never arrives and never gets validated");
    }

    private static string[] Sources() => RepositoryFiles.ProductionSources().ToArray();
}
