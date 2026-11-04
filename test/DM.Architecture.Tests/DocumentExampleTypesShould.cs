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
/// A copy of a real type is never compiled and falls behind it; an example uses a name of its own.
/// Blocks marked csharp, cs or c# are read, against the types declared in src and test.
/// </remarks>
public class DocumentExampleTypesShould
{
    private static readonly string[] CsharpLanguages = ["csharp", "cs", "c#"];

    private static readonly Regex Declaration = new(
        @"\b(?:class|interface|record|struct|enum)\s+(?:class\s+|struct\s+)?(?<name>\p{Lu}\w*)",
        RegexOptions.Compiled);

    private static readonly Regex Comment = new(@"^\s*(?://|/?\*)", RegexOptions.Compiled);

    [Fact]
    public void DeclareNoTypeTheCodeAlreadyHas()
    {
        var declared = CodeTypes();
        declared.Should().NotBeEmpty("a walk that finds no type of the code checks nothing");

        var copies = HandwrittenDocuments.All()
            .SelectMany(document => Declared(document.Lines
                    .Where(line => line.Language is { } language && CsharpLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
                    .Select(line => line.Text))
                .Where(declared.Contains)
                .Select(name => $"{document.Path} :: {name}"));

        string.Join(Environment.NewLine, copies).Should().BeEmpty(
            "an example that declares a real type is an uncompiled copy of it that stops matching the code: " +
            "give the example a name of its own or point at the file");
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
}
