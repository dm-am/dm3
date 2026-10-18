using System;
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

/// <summary>
/// The stand is seeded with markup the grammar accepts.
/// </summary>
/// <remarks>
/// The seed is the first text anybody opening the stand reads, and a broken
/// spelling in it looks like a broken renderer rather than like a typo. It
/// carried [mention="..."], a form neither DM2 nor the grammar has, which the
/// grammar shows as the text it is.
///
/// Two checks, because the seed is written in two ways. The asset files are
/// whole texts, so they are parsed on every surface and no node may come out
/// broken. The texts written inline in the seeding code are pieces of strings
/// and interpolations, which no parser can read whole, so they are searched for
/// the spellings the grammar refuses outright.
/// </remarks>
public class SeedMarkupShould
{
    private static string Seeder => Path.Combine(RepositoryLayout.Root, "src", "DM.Tools.Seeder");

    /// <summary>
    /// Spellings no rendering accepts: a value straight after the name of a tag
    /// that takes none, and [cut], which is no tag.
    /// </summary>
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
