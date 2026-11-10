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
/// A reference is what the compiler accepts, so it cannot be asked about the boundary. Read out of
/// the project files and sources: a project no executable references is invisible to a loader.
/// </remarks>
public class DomainBoundaryShould
{
    private const string Kernel = "DM.Domain.Core";

    private static readonly string[] Infrastructure =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "MongoDB",
        "RabbitMQ",
        "Microsoft.AspNetCore",
        "Serilog",
    ];

    private static readonly Regex Reference = new(
        @"<(?:ProjectReference|PackageReference)\s+Include=""([^""]+)""", RegexOptions.Compiled);


    private static IReadOnlyList<string> DomainProjects =>
        Directory
            .GetDirectories(Path.Combine(RepositoryLayout.Root, "src"), "DM.Domain.*")
            .Select(directory => new DirectoryInfo(directory).Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static string ProjectFile(string project) =>
        Path.Combine(RepositoryLayout.Root, "src", project, project + ".csproj");

    /// <summary>
    /// A rule over an empty set passes, and this set is discovered from the tree, so
    /// a rename that stops matching would otherwise turn every rule below green.
    /// </summary>
    [Fact]
    public void FindEveryDomainProject()
    {
        var projects = DomainProjects;

        projects.Should().Contain(Kernel, "the kernel is a domain project too");
    }

    /// <summary>
    /// The kernel is what all nine point at, so anything it pulls in stands behind
    /// all nine at once.
    /// </summary>
    [Fact]
    public void LeaveTheKernelWithoutASingleReference()
    {
        var references = Reference
            .Matches(File.ReadAllText(ProjectFile(Kernel)))
            .Select(match => match.Groups[1].Value)
            .ToList();

        references.Should().BeEmpty(
            "the comment in the project file calls this the architecture centre, and " +
            "the first reference is the one that makes it stop being one");
    }

    /// <summary>
    /// A module reads another module through a contract in the kernel and writes to it
    /// through an event, and a direct reference is the shape neither of those takes.
    /// </summary>
    [Fact]
    public void ReferenceNoDomainModuleFromAnotherDomainModule()
    {
        var offenders = new List<string>();

        foreach (var project in DomainProjects.Where(name => name != Kernel))
        {
            foreach (Match match in Reference.Matches(File.ReadAllText(ProjectFile(project))))
            {
                var referenced = Path.GetFileNameWithoutExtension(
                    match.Groups[1].Value.Replace('\\', '/'));

                if (referenced.StartsWith("DM.Domain.", StringComparison.Ordinal) &&
                    referenced != Kernel)
                {
                    offenders.Add(project + " -> " + referenced);
                }
            }
        }

        offenders.Should().BeEmpty(
            "one module knowing another by name is what the kernel exists to prevent, " +
            "and the day it happens the two of them stop being separable");
    }

    /// <summary>
    /// The domain states what it needs and the infrastructure answers with a type the
    /// domain declared. A storage or transport namespace inside it is that direction
    /// turned around.
    /// </summary>
    [Fact]
    public void NameNoStorageOrTransportNamespace()
    {
        var root = RepositoryLayout.Root;
        var offenders = new List<string>();
        var read = 0;

        foreach (var project in DomainProjects)
        {
            var folder = Path.Combine(root, "src", project);
            foreach (var source in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).Where(RepositoryFiles.IsAuthored))
            {

                read++;
                var text = File.ReadAllText(source);
                foreach (var forbidden in Infrastructure)
                {
                    if (text.Contains(forbidden + ".", StringComparison.Ordinal))
                    {
                        offenders.Add(Path.GetRelativePath(root, source) + " -> " + forbidden);
                    }
                }
            }
        }

        read.Should().BeGreaterThan(0, "the domain sources are what is being read");
        offenders.Should().BeEmpty(
            "a domain project holds no reference to any of these today, which is why one " +
            "line is all it takes, and the line compiles");
    }
}
