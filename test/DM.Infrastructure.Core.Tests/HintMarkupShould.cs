using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Testing.Markup;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The description of a game tag, the hint surface: its markup is the string
/// the client's preview draws, and it is read with its own tag set on every
/// audience.
/// </summary>
/// <remarks>
/// The pairs are written once, in the client's hintMarkup.cases.ts, and held
/// from both ends: here, that the server renders each input to its string;
/// there, that hintToHtml does. The grammar table says what a text is in
/// neutral pieces; these say what it is in markup, down to the names of the
/// attributes the tooltip reads.
/// </remarks>
public class HintMarkupShould
{
    private const string Begin = "/* hint:begin */";
    private const string End = "/* hint:end */";

    private static string PairsFile => Path.Combine(RepositoryLayout.Root,
        "src", "DM.Web.Client", "src", "shared", "lib", "utils", "hintMarkup.cases.ts");

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    public static TheoryData<int> Pairs() => new(Enumerable.Range(0, ReadPairs().Count));

    [Fact]
    public void ReadEveryPair()
    {
        ReadPairs().Should().HaveCountGreaterThanOrEqualTo(10,
            "the pairs are discovered by reading the file, and a marker that stopped matching " +
            "would leave every check below green over nothing");
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void RenderEveryPairAsTheClientPreviewDraws(int index)
    {
        var (input, html) = ReadPairs()[index];
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Hint);

        wrapper.RenderHtml(input, Reader()).Should().Be(html);
    }

    /// <summary>
    /// Every audience reads the hint with its own set: the safe rendering would
    /// read the tags of a post in it, and the author's the post's markup of the
    /// author's editor, which nothing reads back here.
    /// </summary>
    [Fact]
    public void ReadTheHintWithItsOwnTagsOnEveryAudience()
    {
        const string input = "[b]жирный[/b] [tip text=\"подсказка\"]слово[/tip]";
        const string expected = "[b]жирный[/b] <span class=\"bb-tip\" tabindex=\"0\" " +
                                "data-tip-text=\"подсказка\">слово</span>";

        ((BbParserWrapper)_parserProvider.GetSafeForSurface(BbSurface.Hint))
            .RenderHtml(input, RenderContext.ForEmbedSafe(BbSurface.Hint)).Should().Be(expected);
        ((BbParserWrapper)_parserProvider.GetForAuthorEdit(BbSurface.Hint))
            .RenderHtml(input, Reader()).Should().Be(expected);
        ((BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Hint))
            .RenderText(input, RenderContext.ForPlainText() with { Surface = BbSurface.Hint })
            .Should().Be("[b]жирный[/b] слово");
    }

    /// <summary>
    /// The plain text of a tag the page prints as typed is the tag as typed,
    /// brackets and all, as the parser writes any tag it did not accept.
    /// </summary>
    [Fact]
    public void WriteARefusedTipAsTypedInThePlainText()
    {
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Hint);

        wrapper.RenderText("а [tip img=\"images/a.gif\"]слово[/tip] б",
                RenderContext.ForPlainText() with { Surface = BbSurface.Hint })
            .Should().Be("а [tip img=\"images/a.gif\"]слово[/tip] б");
    }

    /// <summary>
    /// No surface is added without a decision about the checks that go over
    /// every surface: a surface of content is one more for all of them, and
    /// one with a tag set of its own is none of theirs.
    /// </summary>
    [Fact]
    public void EveryBbSurfaceIsDecided()
    {
        MarkupSurfaces.Content.Concat(MarkupSurfaces.Other)
            .Should().BeEquivalentTo(Enum.GetValues<BbSurface>());
        MarkupSurfaces.Content.Should().BeInAscendingOrder(surface => (int)surface,
            "the corpus spreads its texts over the surfaces in this order");
    }

    private static RenderContext Reader() => new()
    {
        Audience = RenderAudience.Display,
        Surface = BbSurface.Hint
    };

    private static IReadOnlyList<(string Input, string Html)> ReadPairs()
    {
        var text = File.ReadAllText(PairsFile);
        var start = text.IndexOf(Begin, StringComparison.Ordinal);
        var end = text.IndexOf(End, StringComparison.Ordinal);
        if (start < 0 || end < start)
            throw new InvalidOperationException($"{PairsFile} lost its {Begin} and {End} markers");

        return JsonNode.Parse(text[(start + Begin.Length)..end])!.AsArray()
            .Select(pair => ((string)pair![0]!, (string)pair[1]!))
            .ToArray();
    }
}
