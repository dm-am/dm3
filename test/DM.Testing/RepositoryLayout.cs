using System;
using System.IO;

namespace DM.Testing;

/// <remarks>The marker is the solution file, which no ancestor carries by accident; found once per process.</remarks>
public static class RepositoryLayout
{
    private const string SolutionFile = "DM.sln";

    public static string Root => RootDirectory.FullName;

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
