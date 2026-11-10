using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// The flag and the cursor are not tied together by anything. Asserted on the initializers: the
/// endpoint needs a database and the defect is visible where the result is built.
/// </remarks>
public class CursorEnvelopeShould
{
    private static readonly Regex Construction = new(
        @"new CursorResult<[^>]+>\s*\{(?<body>[^}]*)\}", RegexOptions.Compiled | RegexOptions.Singleline);

    private static string? Assigned(string body, string member) =>
        Regex.Match(body, $@"\b{member}\s*=\s*(?<value>[^,\r\n]+)") is { Success: true } match
            ? match.Groups["value"].Value.Trim()
            : null;

    [Theory]
    [InlineData("HasPrev", "PrevCursor")]
    [InlineData("HasNext", "NextCursor")]
    public void OfferACursorForEveryPageItClaimsToHave(string flag, string cursor)
    {
        var constructions = RepositoryFiles.ProductionSources()
            .SelectMany(path => Construction
                .Matches(File.ReadAllText(path))
                .Select(match => (Path: RepositoryFiles.Relative(path), Body: match.Groups["body"].Value)))
            .ToList();

        constructions.Should().NotBeEmpty(
            "the repositories build cursor results, and a walk that finds none of them " +
            "passes whatever they say");

        constructions
            // A constant false claims nothing; anything else is a claim that needs a cursor.
            .Where(built => Assigned(built.Body, flag) is not (null or "false"))
            .Where(built => Assigned(built.Body, cursor) is null or "null")
            .Select(built => built.Path)
            .Should().BeEmpty(
                $"{flag} tells a client there is another page and {cursor} is what it would " +
                "ask for that page with, so one without the other is an invitation to a " +
                "request that cannot be made");
    }
}
