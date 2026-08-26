using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// An action documents exactly the status codes it declares.
/// </summary>
/// <remarks>
/// Swashbuckle builds one branch of the published schema out of two sources
/// that nothing joins: the codes come from <c>ProducesResponseType</c>, the
/// sentence describing each comes from a <c>&lt;response&gt;</c> line in the doc
/// comment above it. Where they disagree the schema still builds, and the
/// disagreement leaves the repository in the artefact a client is generated
/// from.
///
/// Both directions had already happened. Activation described a 404 for
/// "token expired or not found" while the service answers 410 and never 404, so
/// a generated client carried a handler for a branch that does not exist and
/// none for the branch that does. Reading a room documented a 404 with no
/// attribute to carry it, so the code vanished from the schema entirely and the
/// most common failure of that endpoint was undocumented.
///
/// Read out of the source rather than out of the built assembly: the doc
/// comment is not compiled into anything this test could reflect over, and the
/// XML file Swashbuckle reads is a build artefact of the same two halves.
/// </remarks>
public class DocumentedResponsesShould
{
    /// <summary>A line of the doc comment naming a status code.</summary>
    private static readonly Regex Documented = new(
        @"<response\s+code=""(\d{3})""", RegexOptions.Compiled);

    /// <summary>
    /// The status code of a declared response type, by name or by bare number.
    /// Matched on a line already known to carry the attribute: the type argument
    /// is <c>typeof(Envelope&lt;Room&gt;)</c> often enough that a pattern reaching
    /// across the whole attribute stops at the wrong bracket.
    /// </summary>
    private static readonly Regex DeclaredCode = new(
        @"StatusCodes\.Status(\d{3})\w*|,\s*(\d{3})\s*\)", RegexOptions.Compiled);

    /// <summary>Where an action's accumulated block ends.</summary>
    private static readonly Regex Signature = new(
        @"^\s*public\s+(?:async\s+)?[\w<>?\[\], .]+\s+\w+\s*\(", RegexOptions.Compiled);

    [Fact]
    public void NameEveryCodeTheyDeclareAndDeclareEveryCodeTheyName()
    {
        var offenders = new List<string>();

        foreach (var file in Controllers())
        {
            var documented = new SortedSet<int>();
            var declared = new SortedSet<int>();
            var line = 0;
            var actionLine = 0;

            foreach (var text in File.ReadAllLines(file))
            {
                line++;

                var documentedHere = Documented.Match(text);
                if (documentedHere.Success)
                {
                    if (documented.Count == 0 && declared.Count == 0) actionLine = line;
                    documented.Add(int.Parse(documentedHere.Groups[1].Value));
                    continue;
                }

                if (text.Contains("ProducesResponseType", StringComparison.Ordinal))
                {
                    if (documented.Count == 0 && declared.Count == 0) actionLine = line;
                    foreach (Match code in DeclaredCode.Matches(text))
                    {
                        var digits = code.Groups[1].Success
                            ? code.Groups[1].Value
                            : code.Groups[2].Value;
                        declared.Add(int.Parse(digits));
                    }

                    continue;
                }

                if (!Signature.IsMatch(text)) continue;

                if (documented.Count > 0 || declared.Count > 0)
                {
                    var undeclared = documented.Except(declared).ToArray();
                    var undocumented = declared.Except(documented).ToArray();

                    if (undeclared.Length > 0)
                    {
                        offenders.Add(
                            $"{Where(file)}:{actionLine} documents {Join(undeclared)} with no " +
                            "ProducesResponseType, so the code is absent from the schema");
                    }

                    if (undocumented.Length > 0)
                    {
                        offenders.Add(
                            $"{Where(file)}:{actionLine} declares {Join(undocumented)} with no " +
                            "<response> line, so the schema carries the code with no description");
                    }
                }

                documented.Clear();
                declared.Clear();
            }
        }

        offenders.Should().BeEmpty(
            "the published schema is what a client is generated from, and a branch " +
            "in it that the server never takes costs the same as a branch the server " +
            "takes that the client never handles");
    }

    private static string Join(IEnumerable<int> codes) => string.Join(", ", codes);

    private static IEnumerable<string> Controllers() => Directory
        .EnumerateFiles(
            Path.Combine(RepositoryRoot, "src", "DM.Web.API"),
            "*Controller.cs",
            SearchOption.AllDirectories)
        .Where(path => !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin"));

    private static string Where(string file) =>
        Path.GetRelativePath(RepositoryRoot, file).Replace('\\', '/');

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
