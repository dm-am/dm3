using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every domain module the tree contains is named by the host that composes them,
/// and every module it names is in the list it actually scans.
/// </summary>
/// <remarks>
/// The composition root scans a hand-written array of marker types. A module added
/// with all its project references and left out of that array compiles, starts, and
/// answers ComponentNotRegisteredException on the first request to one of its
/// endpoints, which is a 500 with a stack trace and no hint of the cause. Nothing in
/// the build says a word, and integration tests only cover the endpoints that have
/// one.
///
/// The irony is on the record: the author of the architecture suite named this exact
/// danger in a comment and scanned a directory instead of listing markers. The
/// composition root kept the list. So the list is read against the tree here, and the
/// markers are written out in full for the same reason a directory scan was chosen
/// there: a name that appears in the source is a name a check can look for.
///
/// Naming was all this checked, and naming is half the step. Declaring the marker
/// and leaving the variable out of the array it feeds registers nothing and reads
/// exactly like the working version — verified by doing it: the module was still
/// named, this rule stayed green, and the container came up without a single
/// service of that module. Both halves are read now.
/// </remarks>
public class ModuleCompositionShould
{
    private const string Kernel = "DM.Domain.Core";

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

    private static string[] DomainModules(string root) => Directory
        .GetDirectories(Path.Combine(root, "src"), "DM.Domain.*")
        .Select(directory => new DirectoryInfo(directory).Name)
        .Where(name => name != Kernel)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    private static string CompositionBody(string root) => Between(
        File.ReadAllText(Path.Combine(root, "src", "DM.Web.API", "Startup.cs")),
        "private static void RegisterDomainServices",
        "\n    }");

    [Fact]
    public void NameEveryDomainModuleTheHostComposes()
    {
        var root = RepositoryRoot;
        var body = CompositionBody(root);
        var modules = DomainModules(root);

        modules.Should().HaveCountGreaterOrEqualTo(8,
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
        var root = RepositoryRoot;
        var body = CompositionBody(root);
        var scanned = Between(body, "var domainAssemblies = new[]", "};");

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

    /// <summary>
    /// Text between an opening marker and the first terminator after it: enough to
    /// read one method, and loud when the method is gone or renamed.
    /// </summary>
    private static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        from.Should().BeGreaterThan(-1, $"the source must still declare {start}");
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        to.Should().BeGreaterThan(-1, $"the declaration of {start} must be terminated");
        return source[from..to];
    }
}
