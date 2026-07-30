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

    /// <summary>
    /// Image markup is emitted from three places — this renderer, the client
    /// renderer and the TipTap node — and they have drifted before: one grew a
    /// data-alt the others never had. This is the server half of the guard; the
    /// client half lives in bbcode.spec.ts. The attribute SET is frozen, not the
    /// byte order: the three legitimately order attributes differently, so a byte
    /// comparison would fail for the wrong reason.
    /// </summary>
    [Theory]
    [InlineData("class=\"bb-image\"")]
    [InlineData("data-bb-tag=\"img\"")]
    [InlineData("loading=\"lazy\"")]
    [InlineData("decoding=\"async\"")]
    [InlineData("referrerpolicy=\"no-referrer\"")]
    public void EmitEveryAgreedImageAttribute(string attribute)
    {
        var tree = _parserProvider.CurrentCommon
            .Parse("[img]https://example.com/image.png[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain(attribute);
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

    /// <summary>
    /// A [link] whose URL is rejected by SanitizeUrl still has to render its
    /// display text, and that text is attacker-controlled: it must be HTML
    /// encoded exactly like the accepted-URL branch encodes it. Emitting it raw
    /// is stored XSS on every content surface (comments, posts, profiles, DMs).
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHN2Zz4=")]
    [InlineData("vbscript:msgbox(1)")]
    public void EncodeLinkText_WhenUrlIsRejected(string dangerousUrl)
    {
        var input = $"[link=<img src=x onerror=alert(1)>]{dangerousUrl}[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var result = ((BbParserWrapper.WrappedNodeTree)tree!).ToHtml();

        result.Should().NotContain("<img src=x onerror=alert(1)>");
        result.Should().Contain("&lt;img src=x onerror=alert(1)&gt;");
    }

    /// <summary>
    /// Same guarantee for the accepted-URL branch, so the two paths cannot
    /// drift apart again.
    /// </summary>
    [Fact]
    public void EncodeLinkText_WhenUrlIsAccepted()
    {
        var input = "[link=<b>bold</b>]https://example.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var result = ((BbParserWrapper.WrappedNodeTree)tree!).ToHtml();

        result.Should().NotContain("<b>bold</b>");
        result.Should().Contain("&lt;b&gt;bold&lt;/b&gt;");
    }

    /// <summary>
    /// Every address here names the machine the post is being read on, or the
    /// network around it, and the request would be issued by the reader's browser.
    /// Four spellings of 127.0.0.1 are listed on purpose: `new Uri` normalises
    /// three of them and leaves the IPv4-mapped one alone, which is precisely
    /// where comparing the host as a string stopped working. The IPv6 rows are
    /// here for the same reason — fd12:: and fe81:: are inside the blocked ranges
    /// while matching none of their common prefixes.
    /// </summary>
    [Theory]
    [InlineData("http://127.0.0.1/x.png")]
    [InlineData("http://127.1/x.png")]
    [InlineData("http://2130706433/x.png")]
    [InlineData("http://[::ffff:127.0.0.1]/x.png")]
    [InlineData("http://[::1]/x.png")]
    [InlineData("http://[fd12:3456::1]/x.png")]
    [InlineData("http://[fe81::1]/x.png")]
    [InlineData("http://[::ffff:192.168.0.1]/x.png")]
    [InlineData("http://[::]/x.png")]
    [InlineData("http://10.0.0.1/x.png")]
    [InlineData("http://172.20.0.1/x.png")]
    [InlineData("http://192.168.0.1/x.png")]
    [InlineData("http://169.254.169.254/x.png")]
    [InlineData("http://100.64.0.1/x.png")]
    [InlineData("http://0.0.0.0/x.png")]
    [InlineData("http://localhost/x.png")]
    [InlineData("http://LOCALHOST/x.png")]
    [InlineData("http://api.localhost/x.png")]
    public void DropImage_WhenUrlPointsIntoReaderNetwork(string url)
    {
        var tree = _parserProvider.CurrentCommon.Parse($"[img]{url}[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().NotContain("<img");
    }

    /// <summary>
    /// The same guard on the [link] path, where a rejected URL costs the anchor
    /// but keeps its text.
    /// </summary>
    [Theory]
    [InlineData("http://[::ffff:127.0.0.1]/probe")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    public void DropAnchor_WhenLinkPointsIntoReaderNetwork(string url)
    {
        var tree = _parserProvider.CurrentCommon.Parse($"[link=tap]{url}[/link]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().NotContain("<a href");
        html.Should().Contain("tap");
    }

    /// <summary>
    /// The other half of the contract: a check that only ever says "no" would
    /// satisfy the theories above while breaking every image in the product. The
    /// last row is a host NAME that merely starts like a private address — the
    /// browser resolves it like any other name, and it was the string comparison,
    /// not the risk, that used to reject it.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/x.png")]
    [InlineData("http://93.184.216.34/x.png")]
    [InlineData("http://[2001:db8::1]/x.png")]
    [InlineData("http://10.example.com/x.png")]
    public void KeepImage_WhenUrlIsPublic(string url)
    {
        var tree = _parserProvider.CurrentCommon.Parse($"[img]{url}[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain($"<img src=\"{url}\"");
    }

    /// <summary>
    /// The placeholder standing in for an extracted [img] used to be spelled
    /// __IMG_0__ — text anyone can type. Restoration replaced every occurrence
    /// of it, so a post carrying those characters next to any image got a second
    /// copy of the image pasted at that spot. Both spellings are checked: the
    /// old one, which is ordinary text now, and the marker itself, which is
    /// removed from the input instead of being honoured.
    /// </summary>
    [Theory]
    [InlineData("__IMG_0__")]
    [InlineData("I0")]
    public void RestoreImage_OnlyWhereItsTagStood(string typedByAuthor)
    {
        var tree = _parserProvider.CurrentCommon
            .Parse($"{typedByAuthor} [img]https://example.com/a.png[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        (html.Split("<img ").Length - 1).Should().Be(1);
    }

    /// <summary>
    /// The same substitution reached places where plain text can never become an
    /// element: a quote author line is built from a tag attribute, so a typed
    /// placeholder there produced an element inside an attribute value.
    /// </summary>
    [Fact]
    public void KeepPlaceholder_WhenAuthorTypedItInsideTagAttribute()
    {
        var tree = _parserProvider.CurrentCommon
            .Parse("[quote=\"__IMG_0__\"]text[/quote][img]https://example.com/a.png[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain("<div class=\"quote-author\">__IMG_0__</div>");
    }

    /// <summary>
    /// A tag attribute is author text and the parser substitutes it into the
    /// markup as it stands, so it has to be encoded before it gets there. Every
    /// surface is listed because they share the [quote] template and the defect
    /// was live on all of them.
    /// </summary>
    [Theory]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.GamePost)]
    [InlineData(BbSurface.GlobalChatMessage)]
    [InlineData(BbSurface.Profile)]
    [InlineData(BbSurface.DirectMessage)]
    public void EncodeQuoteAuthor_OnEverySurface(BbSurface surface)
    {
        var tree = _parserProvider.GetForSurface(surface)
            .Parse("[quote=\"<img src=x onerror=alert(1)>\"]t[/quote]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().NotContain("<img src=x onerror=alert(1)>");
        html.Should().Contain("&lt;img src=x onerror=alert(1)&gt;");
    }

    /// <summary>
    /// The author-edit variant puts the same value into an attribute of its own,
    /// where an unencoded quote closes that attribute and everything after it is
    /// read as attributes of the div — an event handler among them.
    /// </summary>
    [Fact]
    public void EncodePrivateAddressee_OnAuthorEditAttribute()
    {
        var tree = _parserProvider.GetForAuthorEdit(BbSurface.GamePost)
            .Parse("[private=\"a\"onmouseover=alert(1) x=\"\"]s[/private]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain("data-bb-addressees=\"a&quot;onmouseover=alert(1) x=&quot;\"");
    }

    /// <summary>
    /// The other half of that contract: encoding must not reach what the reader
    /// sees. An ordinary name stays itself, and an ampersand in it arrives as one
    /// character in the browser rather than as an entity on the page.
    /// </summary>
    [Fact]
    public void KeepQuoteAuthor_WhenItIsAnOrdinaryName()
    {
        var tree = _parserProvider.CurrentCommon.Parse("[quote=\"Вася & Петя\"]t[/quote]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain("<div class=\"quote-author\">Вася &amp; Петя</div>");
    }

    /// <summary>
    /// A URI carries no raw whitespace, and one that does is what turns a quoted
    /// attribute into two: the second row is the payload that becomes an event
    /// handler on the element the image ends up nested inside.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/a b.png")]
    [InlineData("https://example.com/a.png onmouseover=alert(1) x=")]
    [InlineData("https://example.com/a\tb.png")]
    [InlineData("https://example.com/a\nb.png")]
    public void DropImage_WhenUrlCarriesWhitespace(string url)
    {
        var tree = _parserProvider.CurrentCommon.Parse($"[img]{url}[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().NotContain("<img");
    }

    /// <summary>
    /// And its counterweight: the newlines an editor leaves around a pasted
    /// address are still trimmed, so the rule above costs no working image.
    /// </summary>
    [Fact]
    public void KeepImage_WhenUrlIsPaddedWithNewlines()
    {
        var tree = _parserProvider.CurrentCommon.Parse("[img]\n  https://example.com/a.png\n[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain("<img src=\"https://example.com/a.png\"");
    }
}
