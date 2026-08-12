using System;
using System.IO;

namespace DM.Testing;

/// <summary>
/// The checkout the tests read the tree out of.
/// </summary>
/// <remarks>
/// Whole tiers of this suite assert about the tree itself: sources, documents,
/// workflows, compose files, package manifests, the agent configuration. None of
/// that is copied next to the test binary, and copying it would let a test pass
/// against a snapshot the tree had already moved on from, so those tests walk up
/// from the binary to find the checkout instead.
///
/// The walk used to be written at every one of those sites, once per file, with
/// the marker picked separately each time: "src" and "test" together, or "docs",
/// or "scripts", or "docker", or ".claude". A copy per site is a place per site
/// to fix when the layout moves, and a directory name is a weaker marker than it
/// looks, since the first ancestor that happens to hold a "src" is not by
/// definition this repository.
///
/// The marker here is the solution file: it names this repository, it sits at
/// its root, and it is not something an ancestor directory carries by accident.
/// The walk runs once per test process, because its answer cannot change while
/// one runs.
/// </remarks>
public static class RepositoryLayout
{
    private const string SolutionFile = "DM.sln";

    /// <summary>The root of the checkout the running test binary was built from.</summary>
    public static string Root => RootDirectory.FullName;

    /// <summary>The same root, for the tests that walk down from it.</summary>
    public static DirectoryInfo RootDirectory { get; } = Find();

    private static DirectoryInfo Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, SolutionFile)))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException(
            $"no {SolutionFile} above {AppContext.BaseDirectory}: the tests that read the tree " +
            "need the checkout their binary was built from, and it has to be an ancestor of it");
    }
}
