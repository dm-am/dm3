using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// An action describes exactly the status codes its attributes declare.
/// </summary>
/// <remarks>
/// Swashbuckle builds one response out of two halves that live apart: the code
/// comes from ProducesResponseType and the sentence from the XML comment. A
/// code named by one half alone is invisible in the source and wrong on the
/// wire. The activation endpoint described a 404 next to an attribute that
/// declared 410, so a client generated from the schema got a handler for the
/// branch that cannot happen and none for the one that does, and the room
/// endpoint described a 404 that the schema never published.
///
/// The check is textual because the pairing is textual: nothing compiled holds
/// a comment and an attribute together. Attributes are read line by line,
/// which is how they are written here. One split across lines fails this test
/// rather than passing it silently, and joining it back is the fix.
/// </remarks>
public class DocumentedStatusCodesShould
{
    /// <summary>A code the XML documentation of an action describes.</summary>
    private static readonly Regex Described = new(
        @"<response code=""(\d{3})"">", RegexOptions.Compiled);

    /// <summary>A code the published schema will carry.</summary>
    private static readonly Regex Declared = new(
        @"ProducesResponseType.*?Status(\d{3})", RegexOptions.Compiled);

    /// <summary>
    /// The line that closes a block of documentation and attributes: the member
    /// they belong to.
    /// </summary>
    private static readonly Regex Member = new(
        @"^\s*(?:public|private|protected|internal)\s", RegexOptions.Compiled);

    private static DirectoryInfo RepositoryRoot => DM.Testing.RepositoryLayout.RootDirectory;

    [Fact]
    public void MatchTheAttributesTheSchemaIsBuiltFrom()
    {
        var root = RepositoryRoot;
        var controllers = Directory.GetFiles(
            Path.Combine(root.FullName, "src", "DM.Web.API"),
            "*Controller.cs",
            SearchOption.AllDirectories);

        controllers.Should().NotBeEmpty("the HTTP host is where the controllers live");

        var separator = Path.DirectorySeparatorChar;
        var mismatches = new List<string>();
        var actions = 0;

        foreach (var controller in controllers)
        {
            // Build output: copies of the sources that nobody edits.
            if (controller.Contains($"{separator}obj{separator}", StringComparison.Ordinal) ||
                controller.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(controller);
            var described = new HashSet<string>();
            var declared = new HashSet<string>();
            var start = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var documentation = Described.Match(lines[i]);
                if (documentation.Success)
                {
                    if (described.Count == 0 && declared.Count == 0)
                    {
                        start = i + 1;
                    }

                    described.Add(documentation.Groups[1].Value);
                    continue;
                }

                var attribute = Declared.Match(lines[i]);
                if (attribute.Success)
                {
                    if (described.Count == 0 && declared.Count == 0)
                    {
                        start = i + 1;
                    }

                    declared.Add(attribute.Groups[1].Value);
                    continue;
                }

                if (!Member.IsMatch(lines[i]))
                {
                    continue;
                }

                if (described.Count > 0 || declared.Count > 0)
                {
                    actions++;
                    var withoutAttribute = described.Except(declared).OrderBy(code => code).ToArray();
                    var withoutSentence = declared.Except(described).OrderBy(code => code).ToArray();
                    if (withoutAttribute.Length > 0 || withoutSentence.Length > 0)
                    {
                        mismatches.Add(
                            $"{Path.GetRelativePath(root.FullName, controller)}:{start} " +
                            $"described without an attribute: [{string.Join(", ", withoutAttribute)}], " +
                            $"declared without a description: [{string.Join(", ", withoutSentence)}]");
                    }
                }

                described.Clear();
                declared.Clear();
            }
        }

        // A walk that parsed nothing would leave the assertion below empty.
        actions.Should().BeGreaterThan(300, "the actions of this API carry their responses in both forms");

        mismatches.Should().BeEmpty(
            "the schema takes the code from the attribute and the sentence from the comment, " +
            "so a code named by one of them alone reaches the client as a contract that is not true");
    }
}
