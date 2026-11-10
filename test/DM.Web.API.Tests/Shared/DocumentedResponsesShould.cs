using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Shared;

// Swashbuckle takes the codes from ProducesResponseType and the sentences from <response> lines,
// and nothing joins them.
public class DocumentedResponsesShould
{
    private static readonly Regex Documented = new(
        @"<response\s+code=""(\d{3})""", RegexOptions.Compiled);

    private static readonly Regex DeclaredCode = new(
        @"StatusCodes\.Status(\d{3})\w*|,\s*(\d{3})\s*\)", RegexOptions.Compiled);

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
