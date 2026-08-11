using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The body of a refusal has one author: ErrorHandlingMiddleware.
/// </summary>
/// <remarks>
/// API_DESIGN states the rule and CsrfProtectionMiddleware records why it was
/// adopted: a controller that assembles its own {"error": "..."} adds a shape
/// nothing else answers with, and a client that learns to parse errors once has
/// to special-case it. The webhook endpoint carried exactly that for two of its
/// answers, unnoticed because the endpoint is excluded from the published
/// contract and its own tests only read status codes.
///
/// The check is textual on purpose. The alternative is not expressible in types
/// — an ObjectResult with an anonymous body is a legal return from any action —
/// and it is the shape of the source that the next controller gets copied from.
/// </remarks>
public class ErrorBodyOwnershipShould
{
    /// <summary>
    /// A failure result that carries a body of its own. StatusCode(4xx/5xx, …)
    /// is listed with any argument at all: the overload that takes a value is
    /// the only reason to reach for it, and there is no body it may supply. A
    /// result object over an anonymous type is listed for the same reason and
    /// was the wider hole: the two authorization filters answered every 401 and
    /// every 403 of this host with one, so those two refusals — the only ones no
    /// controller can avoid — went out with no traceId and an English title. A
    /// typed body is a success payload and stays legal.
    /// </summary>
    private static readonly Regex HandBuiltErrorBody = new(
        @"\b(?:BadRequest|NotFound|Conflict|Unauthorized|UnprocessableEntity|Problem|ValidationProblem)\s*\(\s*new\b" +
        @"|\bStatusCode\s*\(\s*(?:[45]\d\d|StatusCodes\.Status[45]\d\d\w*)\s*," +
        @"|\bnew\s+(?:ObjectResult|JsonResult)\s*\(\s*new\s*(?:\{|$)",
        RegexOptions.Compiled);

    /// <summary>
    /// Walks up from the test binary to the repository root. The sources are not
    /// copied to the output directory, and copying them would let this assert
    /// against a stale snapshot.
    /// </summary>
    private static DirectoryInfo RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "docs")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!;
        }
    }

    [Fact]
    public void BeAssembledOnlyByTheMiddleware()
    {
        var root = RepositoryRoot;
        var sources = Directory.GetFiles(
            Path.Combine(root.FullName, "src", "DM.Web.API"), "*.cs", SearchOption.AllDirectories);

        sources.Should().NotBeEmpty("the HTTP host is a C# project");

        var offenders = new List<string>();
        var separator = Path.DirectorySeparatorChar;

        foreach (var source in sources)
        {
            // Build output: generated attribute files and copies of the sources
            // themselves, neither of which anyone edits.
            if (source.Contains($"{separator}obj{separator}", StringComparison.Ordinal) ||
                source.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(source);
            for (var i = 0; i < lines.Length; i++)
            {
                if (HandBuiltErrorBody.IsMatch(lines[i]))
                {
                    offenders.Add(Path.GetRelativePath(root.FullName, source) + ":" + (i + 1));
                }
            }
        }

        offenders.Should().BeEmpty(
            "a refusal is thrown as HttpException and shaped once, by ErrorHandlingMiddleware");
    }
}
