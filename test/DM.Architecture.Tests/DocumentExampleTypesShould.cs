using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// An example of C# in a document does not declare a type the code already has.
/// </summary>
/// <remarks>
/// An example that declares IGameService next to the real IGameService is a
/// second definition of it, written once and never compiled. The real one moves
/// on, the copy does not, and the reader cannot tell which members are the
/// convention and which were simply there on the day the example was typed:
/// PATTERNS.md showed an IGameService the code no longer had, and TESTING.md a
/// TopicServiceShould unlike the one in the tests. An example illustrates a
/// convention with a name of its own (IWidgetService), and a real type is shown
/// by naming its file, which DocumentedPathsShould keeps true.
///
/// The blocks read are the ones marked csharp, cs or c#, and a declaration is
/// class, interface, record, struct or enum followed by a name. The names of the
/// code are those declared in the C# files of src and test, lines of comment left
/// out. Other languages are not compared: a TypeScript interface named after a
/// C# class is the client's mirror of it, not a copy of the server's.
///
/// The examples that copy a type on the day the gate arrived are recorded by
/// document and name; the register only shrinks (see
/// <see cref="HandwrittenDocuments"/>).
/// </remarks>
public class DocumentExampleTypesShould
{
    private static readonly string[] CsharpLanguages = ["csharp", "cs", "c#"];

    private static readonly Regex Declaration = new(
        @"\b(?:class|interface|record|struct|enum)\s+(?:class\s+|struct\s+)?(?<name>\p{Lu}\w*)",
        RegexOptions.Compiled);

    private static readonly Regex Comment = new(@"^\s*(?://|/?\*)", RegexOptions.Compiled);

    /// <summary>An example declaring a type the code has.</summary>
    /// <param name="File">Path from the repository root, forward slashes.</param>
    /// <param name="Type">The name the example declares.</param>
    private sealed record Recorded(string File, string Type);

    private static readonly Recorded[] Register = [];

    [Fact]
    public void DeclareNoTypeTheCodeAlreadyHas()
    {
        var declared = CodeTypes();
        declared.Should().HaveCountGreaterThan(500, "the code declares well over a thousand types");

        var measured = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var document in HandwrittenDocuments.All())
        {
            var examples = document.Lines
                .Where(line => line.Language is { } language && CsharpLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
                .Select(line => line.Text);
            foreach (var name in Declared(examples).Where(declared.Contains))
            {
                measured[Key(document.Path, name)] = 1;
            }
        }

        var problems = HandwrittenDocuments.Disagreements(
            Register.ToDictionary(entry => Key(entry.File, entry.Type), _ => 1, StringComparer.Ordinal),
            measured,
            (key, _) => $"new(\"{key.Replace(" :: ", "\", \"", StringComparison.Ordinal)}\"),");

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "an example that declares a real type is an uncompiled copy of it that stops matching the code: " +
            "give the example a name of its own or point at the file, and drop the record once it is gone");
    }

    [Theory]
    [InlineData("public interface IGameService", "IGameService")]
    [InlineData("internal sealed record class GameDetails(Guid Id);", "GameDetails")]
    [InlineData("public enum GameStatus { Draft }", "GameStatus")]
    [InlineData("// the class Game is shown below", "")]
    [InlineData("var record = new Game();", "")]
    public void ReadTheNameADeclarationGives(string line, string name) =>
        string.Join(",", Declared([line])).Should().Be(name);

    /// <summary>The names the lines declare, lines of comment left out.</summary>
    private static IEnumerable<string> Declared(IEnumerable<string> lines) => lines
        .Where(line => !Comment.IsMatch(line))
        .SelectMany(line => Declaration.Matches(line).Select(match => match.Groups["name"].Value))
        .Distinct(StringComparer.Ordinal);

    private static HashSet<string> CodeTypes()
    {
        var root = DM.Testing.RepositoryLayout.Root;
        return RepositoryFiles.CommittableFiles()
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => HandwrittenDocuments.Relative(path) is var relative &&
                           (relative.StartsWith("src/", StringComparison.Ordinal) ||
                            relative.StartsWith("test/", StringComparison.Ordinal)))
            .SelectMany(path => Declared(File.ReadLines(path)))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Key(string file, string type) => $"{file} :: {type}";
}
