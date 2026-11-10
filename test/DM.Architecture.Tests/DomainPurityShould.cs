using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Asserted against assembly references: a type reached through an alias leaves nothing in the file.
/// The DI abstractions are left out: each module composes its own registrations.
/// </remarks>
public class DomainPurityShould
{
    /// <summary>
    /// Prefixes, so a sibling of a package that has already been through here once
    /// (Npgsql.EntityFrameworkCore.PostgreSQL, Serilog.Sinks.Console,
    /// Microsoft.AspNetCore.Http.Abstractions) is caught by the same entry.
    /// </summary>
    private static readonly string[] Infrastructure =
    [
        "Npgsql",
        "MongoDB",
        "Serilog",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "DM.Infrastructure",
    ];


    /// <summary>
    /// Discovered in the output directory rather than named by a marker type each:
    /// the host drags every domain assembly here, and a hand-written list would
    /// quietly stop covering the one added next.
    /// </summary>
    private static Assembly[] DomainAssemblies() => Directory
        .GetFiles(AppContext.BaseDirectory, "DM.Domain.*.dll", SearchOption.TopDirectoryOnly)
        .Select(Assembly.LoadFrom)
        .ToArray();

    /// <summary>
    /// A rule that matches nothing passes. An assembly that stopped reaching the
    /// output directory would leave the rule below green while covering less, so
    /// what was found is compared with what the solution declares.
    /// </summary>
    [Fact]
    public void CoverEveryDomainProjectOfTheSolution()
    {
        var declared = Directory
            .GetDirectories(Path.Combine(RepositoryLayout.Root, "src"), "DM.Domain.*")
            .Select(directory => new DirectoryInfo(directory).Name)
            .ToArray();

        var loaded = DomainAssemblies()
            .Select(assembly => assembly.GetName().Name)
            .ToArray();

        loaded.Should().BeEquivalentTo(declared,
            "an assembly missing from the output directory is one this class checks nothing about");
    }

    [Fact]
    public void NameNoInfrastructureAssembly()
    {
        var offenders = DomainAssemblies()
            .SelectMany(domain => domain
                .GetReferencedAssemblies()
                .Where(reference => Infrastructure.Any(prefix =>
                    reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
                .Select(reference => $"{domain.GetName().Name} -> {reference.Name}"))
            .OrderBy(offender => offender, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "the domain holds the rules of the game, not the machinery that stores " +
            "or ships them, see the class remarks for what this closes");
    }

    // A repository offering them spreads one unit of work across contracts the retry strategy does
    // not wrap.
    private static readonly string[] StoragePrimitives =
        ["SaveChanges", "BeginTransaction", "CommitTransaction", "RollbackTransaction", "Flush"];

    // A bare SaveChanges commits whatever another repository left tracked on the shared context.
    [Fact]
    public void AskForOutcomesRatherThanForStepsOfATransaction()
    {
        var contracts = DomainAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsInterface)
            .ToArray();

        contracts.Should().NotBeEmpty(
            "the domain declares its contracts as interfaces, and finding none passes " +
            "whatever they declare");

        contracts
            .SelectMany(contract => contract
                .GetMethods()
                .Where(method => StoragePrimitives.Contains(method.Name, StringComparer.Ordinal))
                .Select(method => $"{contract.Name}.{method.Name}"))
            .OrderBy(offender => offender, StringComparer.Ordinal)
            .Should().BeEmpty(
                "these are steps of a unit of work, and a contract offering one lets a caller " +
                "commit whatever another repository left tracked on the shared context, at a " +
                "moment nobody chose and outside the strategy that retries it");
    }
}
