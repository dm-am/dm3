using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// <remarks>The pairs are written once, in hintMarkup.cases.ts, and held from both ends.</remarks>
public class HintMarkupShould
{

    private static string PairsFile => Path.Combine(RepositoryLayout.Root,
        "src", "DM.Web.Client", "src", "shared", "lib", "bbcode", "hintMarkup.cases.ts");

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    public static TheoryData<int> Pairs() => new(Enumerable.Range(0, ReadPairs().Count));

    [Theory]
    [MemberData(nameof(Pairs))]
    public void RenderEveryPairAsTheClientPreviewDraws(int index)
    {
        var (input, html) = ReadPairs()[index];
        var wrapper = _parserProvider.GetForSurface(BbSurface.Hint);

        wrapper.RenderHtml(input, Reader()).Should().Be(html);
    }

    [Fact]
    public void ReadTheHintWithItsOwnTagsOnEveryAudience()
    {
        const string input = "[b]жирный[/b] [tip text=\"подсказка\"]слово[/tip]";
        const string expected = "[b]жирный[/b] <span class=\"bb-tip\" tabindex=\"0\" " +
                                "data-tip-text=\"подсказка\">слово</span>";

        _parserProvider.GetSafeForSurface(BbSurface.Hint)
            .RenderHtml(input, RenderContext.ForEmbedSafe(BbSurface.Hint)).Should().Be(expected);
        _parserProvider.GetForSurface(BbSurface.Hint)
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
        var wrapper = _parserProvider.GetForSurface(BbSurface.Hint);

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

    private static IReadOnlyList<(string Input, string Html)> ReadPairs() => ClientTable.Read(PairsFile, "hint").AsArray()
        .Select(pair => ((string)pair![0]!, (string)pair[1]!))
        .ToArray();
}
