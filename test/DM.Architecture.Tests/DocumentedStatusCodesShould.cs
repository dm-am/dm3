using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// An action describes exactly the status codes its attributes declare.
/// </summary>
/// <remarks>
/// Swashbuckle takes the code from ProducesResponseType and the sentence from the XML comment.
/// Textual, line by line: an attribute split across lines fails this.
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


    /// <summary>
    /// Every block of documentation and attributes in a controller that names a
    /// code, closed by its member: the line the block starts on, the codes it
    /// describes and the codes it declares.
    /// </summary>
    private static IEnumerable<(int Start, IReadOnlySet<string> Described, IReadOnlySet<string> Declared)>
        Actions(string[] lines)
    {
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
                yield return (start, described, declared);
            }

            described = new HashSet<string>();
            declared = new HashSet<string>();
        }
    }

    [Fact]
    public void MatchTheAttributesTheSchemaIsBuiltFrom()
    {
        var root = RepositoryLayout.RootDirectory;
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

            foreach (var (start, described, declared) in Actions(File.ReadAllLines(controller)))
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
        }

        actions.Should().BePositive("a walk that parsed nothing would leave the assertion below empty");

        mismatches.Should().BeEmpty(
            "the schema takes the code from the attribute and the sentence from the comment, " +
            "so a code named by one of them alone reaches the client as a contract that is not true");
    }
}
