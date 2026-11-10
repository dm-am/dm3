using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using BBCodeParser.Nodes;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Testing.Markup;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

// The asset files are whole texts and are parsed on every surface; the inline texts are pieces of
// strings and are searched for the spellings the grammar refuses.
public class SeedMarkupShould
{
    private static string Seeder => Path.Combine(RepositoryLayout.Root, "src", "DM.Tools.Seeder");

    private static readonly Regex Refused = new(
        @"\[(?:mention|img|link|spoiler|quote|code|noparse|b|i|u|strike|nsfw|ul|ol|li|tab|mod)=|\[/?cut\]|\[private=[^""\\]",
        RegexOptions.Compiled);

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    [Fact]
    public void ParseEveryAssetWithoutABrokenTag()
    {
        var assets = Directory.GetFiles(Path.Combine(Seeder, "Assets"), "*.bbcode", SearchOption.AllDirectories);
        assets.Should().NotBeEmpty("the seed keeps its long texts as assets");

        var broken = new List<string>();
        foreach (var asset in assets)
        {
            var text = File.ReadAllText(asset);
            foreach (var surface in MarkupSurfaces.Content)
            {
                broken.AddRange(Walk(_parserProvider.GetForSurface(surface).Parse(text))
                    .OfType<TagNode>()
                    .Where(node => node.IsBroken)
                    .Select(node => $"{Path.GetFileName(asset)} on {surface}: {node.OpeningText}"));
            }
        }

        broken.Should().BeEmpty();
    }

    [Fact]
    public void WriteNoRefusedSpellingInTheSeedingCode()
    {
        var sources = Directory.GetFiles(Path.Combine(Seeder, "Seeding"), "*.cs", SearchOption.AllDirectories);
        sources.Should().NotBeEmpty("the seeding code is where the rule looks");

        var found = sources
            .SelectMany(source => File.ReadAllLines(source)
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(line => Refused.IsMatch(line.Line))
                .Select(line => $"{Path.GetFileName(source)}:{line.Number}: {line.Line.Trim()}"))
            .ToArray();

        found.Should().BeEmpty();
    }

    private static IEnumerable<Node> Walk(Node root)
    {
        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            yield return node;
            foreach (var child in node.Children) pending.Push(child);
        }
    }
}
