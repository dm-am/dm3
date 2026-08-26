using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A refusal the caller can act on, not one the host reports as its own fault.
/// </summary>
/// <remarks>
/// The error middleware maps HttpException and its kin and nothing else, so any
/// other exception reaches the client as 500 with a critical log line. Twelve
/// authorization checks across moderation, the profile notes and the session
/// endpoint were written as UnauthorizedAccessException: every one of them
/// answered a request that merely lacked rights by reporting a broken server,
/// and the neighbouring checks in the same files already threw HttpException.
///
/// One of the twelve was worse than a wrong status. The profile page caught the
/// exception to learn that its viewer was anonymous — control flow across a
/// layer boundary, where a nullable return already said the same thing.
///
/// Scanned as source because the alternative is calling every guard in the
/// solution with an unauthenticated identity, and the property is a syntactic
/// one: the type is either named in a throw or it is not.
/// </remarks>
public class DomainRefusalsShould
{
    /// <summary>Exceptions that reach the client as a server fault.</summary>
    private static readonly string[] Unmapped = { "UnauthorizedAccessException" };

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static IEnumerable<string> SourceFiles() => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void BeThrownAsSomethingTheErrorMiddlewareUnderstands()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles())
        {
            // Read as code: a paragraph naming the exception is not a throw of it.
            var source = SourceText.ReadCode(file);
            foreach (var unmapped in Unmapped)
            {
                if (!source.Contains($"throw new {unmapped}", StringComparison.Ordinal))
                {
                    continue;
                }

                offenders.Add($"{Path.GetRelativePath(RepositoryRoot, file)}: {unmapped}");
            }
        }

        offenders.Should().BeEmpty(
            "the middleware maps HttpException and its kin, so anything else answers a " +
            "caller who merely lacks rights with 500 and a critical log line");
    }

    [Fact]
    public void FindTheFilesToScan() =>
        SourceFiles().Should().HaveCountGreaterThan(200,
            "a file walk that stops matching turns the rule above green by checking nothing");
}
