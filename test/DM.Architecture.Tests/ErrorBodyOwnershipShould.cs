using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// API_DESIGN states the rule. Textual: an ObjectResult with an anonymous body is a legal return
/// from any action.
/// </remarks>
public class ErrorBodyOwnershipShould
{
    /// <summary>
    /// A failure result that carries a body of its own: StatusCode(4xx/5xx, ...) with any argument, or a
    /// result object over an anonymous type. A typed body is a success payload and stays legal.
    /// </summary>
    private static readonly Regex HandBuiltErrorBody = new(
        @"\b(?:BadRequest|NotFound|Conflict|Unauthorized|UnprocessableEntity|Problem|ValidationProblem)\s*\(\s*new\b" +
        @"|\bStatusCode\s*\(\s*(?:[45]\d\d|StatusCodes\.Status[45]\d\d\w*)\s*," +
        @"|\bnew\s+(?:ObjectResult|JsonResult)\s*\(\s*new\s*(?:\{|$)",
        RegexOptions.Compiled);


    [Fact]
    public void BeAssembledOnlyByTheMiddleware()
    {
        var root = RepositoryLayout.RootDirectory;
        var sources = Directory
            .GetFiles(Path.Combine(root.FullName, "src", "DM.Web.API"), "*.cs", SearchOption.AllDirectories)
            .Where(RepositoryFiles.IsAuthored)
            .ToArray();

        sources.Should().NotBeEmpty("the HTTP host is a C# project");

        var offenders = new List<string>();
        foreach (var source in sources)
        {
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
