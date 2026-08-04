using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A job that repeats runs on the one loop, not on a copy of it.
/// </summary>
/// <remarks>
/// Nine services each carried the same twenty lines - a timer, a while over the
/// stopping token, a catch for cancellation, a catch for everything else, a
/// scope per pass - and the copies had drifted apart in every way they could.
/// Six ran their first pass outside the guard, one inside, one had none; two
/// waited before the first pass and seven did not; one duplicated the tick wait
/// inside its own error branch. The drift is not cosmetic: work placed before
/// the first await runs inside host startup, and a cancellation raised there
/// leaves ExecuteAsync, which BackgroundService reports as a critical failure
/// and answers by stopping the host - once per deployment rolled back inside its
/// first minute.
///
/// Held by the timer, because the timer is what a hand-rolled loop needs and the
/// shared one already owns. A tenth job written the old way brings its own.
/// </remarks>
public class PeriodicJobShould
{
    private const string BaseType = "PeriodicHostedService";

    [Fact]
    public void FindThePeriodicJobs() =>
        Jobs().Should().HaveCountGreaterOrEqualTo(9,
            "the API host runs nine of them, and a smaller match means the assemblies were " +
            "never loaded and the rule below checks nothing");

    [Fact]
    public void OwnTheTimerInOnePlace() =>
        Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(IsAuthored)
            .Where(path => File.ReadAllText(path).Contains("new PeriodicTimer(", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .Should().BeEquivalentTo([$"{BaseType}.cs"],
                "a job that builds its own timer has its own loop around it, and the loop is " +
                "where the cancellation handling and the scope live");

    private static IReadOnlyCollection<Type> Jobs() => ProductionTypes()
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

    private static IEnumerable<Type> ProductionTypes() => Directory
        .EnumerateFiles(AppContext.BaseDirectory, "DM.*.dll", SearchOption.TopDirectoryOnly)
        .Where(path => !Path.GetFileNameWithoutExtension(path)
            .EndsWith(".Tests", StringComparison.Ordinal))
        .SelectMany(TypesOf);

    private static IEnumerable<Type> TypesOf(string assemblyPath)
    {
        try
        {
            return Assembly.LoadFrom(assemblyPath).GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(type => type is not null).Select(type => type!);
        }
        catch (Exception)
        {
            return Array.Empty<Type>();
        }
    }

    private static bool IsAuthored(string path) => !path
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin" or "node_modules");

    /// <summary>
    /// Walks up from the test binary to the repository root. The sources are not
    /// copied to the output directory, and copying them would let this assert
    /// against a stale snapshot.
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
}
