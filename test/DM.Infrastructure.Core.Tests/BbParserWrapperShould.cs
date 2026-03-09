using DM.Infrastructure.Core.Parsing;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

public class BbParserWrapperShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    [Fact]
    public void PreserveImgAndLinkTags_ToBb()
    {
        var input = "[img]https://example.com/image.png[/img]\n\n[link]https://example.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        wrapped.Should().NotBeNull("Parse should return WrappedNodeTree");

        var result = wrapped!.ToBb();
        result.Should().Contain("[img]https://example.com/image.png[/img]");
        result.Should().Contain("[link]https://example.com[/link]");
    }

    [Fact]
    public void PreserveImgAndLinkTags_ToHtml()
    {
        var input = "[img]https://example.com/image.png[/img]\n\n[link]https://example.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        wrapped.Should().NotBeNull("Parse should return WrappedNodeTree");

        var result = wrapped!.ToHtml();
        result.Should().Contain("<img src=\"https://example.com/image.png\"");
        result.Should().Contain("<a href=\"https://example.com\"");
    }

    [Fact]
    public void PreserveLinkWithText_ToBb()
    {
        var input = "[link=Click here]https://example.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        var result = wrapped!.ToBb();
        result.Should().Be("[link=Click here]https://example.com[/link]");
    }

    [Fact]
    public void PreserveLinkWithText_ToHtml()
    {
        var input = "[link=Click here]https://example.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        var result = wrapped!.ToHtml();
        result.Should().Contain("<a href=\"https://example.com\"");
        result.Should().Contain(">Click here</a>");
    }

    [Fact]
    public void PreserveMixedContent()
    {
        var input = "[b]Bold text[/b] with [img]https://img.com/a.png[/img] and [link]https://link.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        var result = wrapped!.ToBb();

        result.Should().Contain("[b]Bold text[/b]");
        result.Should().Contain("[img]https://img.com/a.png[/img]");
        result.Should().Contain("[link]https://link.com[/link]");
    }

    [Fact]
    public void HandleMultipleImagesAndLinks()
    {
        var input = "[img]https://img1.com[/img][img]https://img2.com[/img][link]https://link1.com[/link][link=text]https://link2.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        var result = wrapped!.ToBb();

        result.Should().Contain("[img]https://img1.com[/img]");
        result.Should().Contain("[img]https://img2.com[/img]");
        result.Should().Contain("[link]https://link1.com[/link]");
        result.Should().Contain("[link=text]https://link2.com[/link]");
    }

    // NOTE: [spoiler=title] is NOT supported - only simple [spoiler] is handled by BBCodeParser

    [Fact]
    public void ConvertSimpleSpoiler_ToHtml_UsesDefaultText()
    {
        var input = "[spoiler]hidden content[/spoiler]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        var result = wrapped!.ToHtml();

        result.Should().Contain("class=\"spoiler-head\"");
        result.Should().Contain($">{BbParserWrapper.DefaultSpoilerText}</a>");
        result.Should().Contain("hidden content");
    }
}
