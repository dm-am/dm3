using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every domain module the tree contains is named by the host that composes them,
/// and every module it names is in the list it actually scans.
/// </summary>
/// <remarks>
/// A module left out of the hand-written array of markers compiles and answers 500 on its first
/// request. Both halves are read: the marker is named and its variable reaches the array.
/// </remarks>
public class ModuleCompositionShould
{
    private const string Kernel = "DM.Domain.Core";


    private static string[] DomainModules(string root) => Directory
        .GetDirectories(Path.Combine(root, "src"), "DM.Domain.*")
        .Select(directory => new DirectoryInfo(directory).Name)
        .Where(name => name != Kernel)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    private static string CompositionBody(string root) => SourceText.Between(
        File.ReadAllText(Path.Combine(root, "src", "DM.Web.API", "Startup.cs")),
        "private static void RegisterDomainServices",
        "\n    }");

    [Fact]
    public void NameEveryDomainModuleTheHostComposes()
    {
        var root = RepositoryLayout.Root;
        var body = CompositionBody(root);
        var modules = DomainModules(root);

        modules.Should().NotBeEmpty(
            "the modules are discovered from the tree, and a filter that stops matching " +
            "would leave this rule with nothing to check");

        var missing = modules
            .Where(module => !body.Contains(module + ".", StringComparison.Ordinal))
            .ToList();

        missing.Should().BeEmpty(
            "a module the composition root does not name has no services registered, and " +
            "the first request to it answers 500 from the container rather than from the " +
            "code anybody wrote");
    }

    /// <summary>
    /// The marker of every module reaches the array the host scans.
    /// </summary>
    [Fact]
    public void ScanTheMarkerOfEveryModuleItNames()
    {
        var root = RepositoryLayout.Root;
        var body = CompositionBody(root);
        var scanned = SourceText.Between(body, "var domainAssemblies = new[]", "};");

        scanned.Should().NotBeNullOrWhiteSpace("the array of scanned assemblies must still be declared");

        var unscanned = DomainModules(root)
            .Select(module => (Module: module, Marker: MarkerVariable(body, module)))
            .Where(pair => pair.Marker == null ||
                           !Regex.IsMatch(scanned, $@"\b{Regex.Escape(pair.Marker)}\b"))
            .Select(pair => pair.Module + " -> " + (pair.Marker ?? "no marker variable"))
            .ToList();

        unscanned.Should().BeEmpty(
            "a marker declared and left out of the array registers nothing at all, and the " +
            "module reads as composed in every place a person would look");
    }

    /// <summary>
    /// Name of the local the module's marker type is assigned to, or null when the
    /// module is not declared as a marker at all.
    /// </summary>
    private static string? MarkerVariable(string body, string module)
    {
        var match = Regex.Match(
            body, $@"var\s+(?<name>\w+)\s*=\s*typeof\({Regex.Escape(module)}\.");
        return match.Success ? match.Groups["name"].Value : null;
    }
}
