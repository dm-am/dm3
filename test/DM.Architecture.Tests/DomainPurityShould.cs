using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A domain assembly names no infrastructure: not the database driver, not the
/// document store, not the broker client, not the logging library, not the web
/// framework, not the persistence project.
/// </summary>
/// <remarks>
/// This is the breach that came back the most. Serilog reached DM.Domain.Account
/// through a log context push, and Npgsql reached DM.Domain.Community and
/// DM.Domain.Game because three services caught PostgresException to read a
/// SQLSTATE. Both were unwound afterwards by a read of the whole tree rather than
/// by a gate, because nothing was watching: the boundary rules next door cover
/// the controller side only, and this rule lived in a review checklist, which is
/// read by a reviewer and not by a build.
///
/// Asserted against assembly references rather than against using directives. A
/// type reached through an alias or a fully qualified name leaves nothing in the
/// file for a textual check to find, while the compiler records the reference
/// either way. The same property is why a package that is referenced and never
/// touched stays invisible here: it costs the domain nothing until a type from it
/// is used.
///
/// Autofac is deliberately absent from the list. Each module composes its own
/// registrations, so the container's builder is part of the shape a domain
/// assembly publishes rather than machinery leaking into it.
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
        "RabbitMQ",
        "Jamq",
        "DM.Infrastructure",
    ];

    /// <summary>
    /// Walks up from the test binary to the repository root. The project list is
    /// read from the tree, and copying it to the output directory would let this
    /// assert against a stale snapshot.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !(Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                     Directory.Exists(Path.Combine(directory.FullName, "test"))))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }

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
            .GetDirectories(Path.Combine(RepositoryRoot, "src"), "DM.Domain.*")
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
}
