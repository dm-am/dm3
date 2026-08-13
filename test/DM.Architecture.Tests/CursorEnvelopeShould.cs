using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A page that says there is more says what to ask for it with.
/// </summary>
/// <remarks>
/// The two halves of a cursor envelope are a flag and a cursor, and nothing ties
/// them together. Message search reported HasPrev from the second page on while
/// its PrevCursor stayed null — because it walks forward only and has no reverse
/// cursor to give — so any client reading the contract as written was told there
/// was a page back and handed nothing to ask for it with. Nothing failed: the
/// endpoint answered, the page was correct, and the flag was a lie only a client
/// that tried to act on it would find.
///
/// Asserted on the initializers rather than on a running endpoint, because the
/// endpoint needs a database and the defect is entirely visible in the four lines
/// that build the result.
/// </remarks>
public class CursorEnvelopeShould
{
    /// <summary>One construction of a cursor result, from the new to its brace.</summary>
    private static readonly Regex Construction = new(
        @"new CursorResult<[^>]+>\s*\{(?<body>[^}]*)\}", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>How a member of that initializer is assigned.</summary>
    private static string? Assigned(string body, string member) =>
        Regex.Match(body, $@"\b{member}\s*=\s*(?<value>[^,\r\n]+)") is { Success: true } match
            ? match.Groups["value"].Value.Trim()
            : null;

    [Theory]
    [InlineData("HasPrev", "PrevCursor")]
    [InlineData("HasNext", "NextCursor")]
    public void OfferACursorForEveryPageItClaimsToHave(string flag, string cursor)
    {
        var constructions = Sources()
            .SelectMany(path => Construction
                .Matches(File.ReadAllText(path))
                .Select(match => (Path: Relative(path), Body: match.Groups["body"].Value)))
            .ToList();

        constructions.Should().NotBeEmpty(
            "the repositories build cursor results, and a walk that finds none of them " +
            "passes whatever they say");

        constructions
            // A flag that is a constant false claims nothing, and the cursor beside
            // it is allowed to be null. Anything else - a variable, a condition - is
            // a claim, and a claim needs something to act on.
            .Where(built => Assigned(built.Body, flag) is not (null or "false"))
            .Where(built => Assigned(built.Body, cursor) is null or "null")
            .Select(built => built.Path)
            .Should().BeEmpty(
                $"{flag} tells a client there is another page and {cursor} is what it would " +
                "ask for that page with, so one without the other is an invitation to a " +
                "request that cannot be made");
    }

    private static IEnumerable<string> Sources() => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(path => !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin"));

    private static string Relative(string path) => Path.GetRelativePath(RepositoryRoot, path);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
