using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A job that repeats runs on the one loop, not on a copy of it.
/// </summary>
/// <remarks>
/// Work before the first await runs inside host startup, and a cancellation there stops the host.
/// Held by the timer, which only a hand-rolled loop needs.
/// </remarks>
public class PeriodicJobShould
{
    private const string BaseType = "PeriodicHostedService";

    [Fact]
    public void FindThePeriodicJobs() =>
        Jobs().Should().NotBeEmpty(
            "no match means the assemblies were never loaded and the rule below checks nothing");

    [Fact]
    public void OwnTheTimerInOnePlace() =>
        RepositoryFiles.ProductionSources()
            .Where(path => File.ReadAllText(path).Contains("new PeriodicTimer(", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .Should().BeEquivalentTo([$"{BaseType}.cs"],
                "a job that builds its own timer has its own loop around it, and the loop is " +
                "where the cancellation handling and the scope live");

    private static IReadOnlyCollection<Type> Jobs() => ProductionAssemblies.Types()
        .Where(type => type is { IsAbstract: false, IsClass: true })
        .Where(type => Ancestry(type).Any(ancestor =>
            string.Equals(ancestor.Name, BaseType, StringComparison.Ordinal)))
        .ToArray();

    private static IEnumerable<Type> Ancestry(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }
}
