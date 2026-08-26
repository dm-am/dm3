using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

public class BbParserWrapperShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    [Fact]
    public void PreserveImgAndLinkTags_ToBb()
    {
        var input = "[img]https://example.com/image.png[/img]\n\n[link]https://example.com[/link]";
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);
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
        var tree = _parserProvider.GetForSurface(BbSurface.Comment)
            .Parse("[img]https://example.com/image.png[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain(attribute);
    }

    [Fact]
    public void PreserveImgAndLinkTags_ToHtml()
    {
        var input = "[img]https://example.com/image.png[/img]\n\n[link]https://example.com[/link]";
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);
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
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        var result = wrapped!.ToBb();
        result.Should().Be("[link=Click here]https://example.com[/link]");
    }

    [Fact]
    public void PreserveLinkWithText_ToHtml()
    {
        var input = "[link=Click here]https://example.com[/link]";
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);
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
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);
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
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);
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
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);
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
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);
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
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);
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
        var tree = _parserProvider.GetForSurface(BbSurface.Comment).Parse($"[img]{url}[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().NotContain("<img");
    }

    /// <summary>
    /// The attribute spelling of the tag is the same tag.
    /// </summary>
    /// <remarks>
    /// [img="URL"] carries the address as the attribute, so none of the patterns
    /// that extract the content forms matched it and the tag went straight to
    /// the inner parser, which puts the value into its template as written.
    /// Every guard on this path lives in the wrapper, so the whole of it — the
    /// scheme check, the addresses below, the spoiler gate, referrerpolicy and
    /// lazy loading — was skipped by a spelling anybody can type.
    /// </remarks>
    [Theory]
    [InlineData("http://127.0.0.1/x.png")]
    [InlineData("http://169.254.169.254/x.png")]
    [InlineData("http://192.168.0.1/x.png")]
    [InlineData("http://10.0.0.1/x.png")]
    [InlineData("http://localhost/x.png")]
    [InlineData("ftp://evil.example/x.png")]
    public void DropImage_WhenTheAttributeFormPointsIntoReaderNetwork(string url)
    {
        var quoted = ((BbParserWrapper.WrappedNodeTree)_parserProvider.GetForSurface(BbSurface.Comment)
            .Parse($"[img=\"{url}\"]")).ToHtml();
        var bare = ((BbParserWrapper.WrappedNodeTree)_parserProvider.GetForSurface(BbSurface.Comment)
            .Parse($"[img={url}]")).ToHtml();

        quoted.Should().NotContain("<img");
        bare.Should().NotContain("<img");
    }

    /// <summary>
    /// An ordinary address written the attribute way renders like the canonical
    /// spelling, guards and all.
    /// </summary>
    [Fact]
    public void RenderTheAttributeFormThroughTheSamePathAsTheContentForm()
    {
        const string url = "https://example.org/picture.png";

        var attribute = ((BbParserWrapper.WrappedNodeTree)_parserProvider.GetForSurface(BbSurface.Comment)
            .Parse($"[img=\"{url}\"]")).ToHtml();
        var content = ((BbParserWrapper.WrappedNodeTree)_parserProvider.GetForSurface(BbSurface.Comment)
            .Parse($"[img]{url}[/img]")).ToHtml();

        attribute.Should().Be(content);
    }

    /// <summary>
    /// A size is still a size: [img=200]…[/img] names a width, not an address.
    /// </summary>
    [Theory]
    [InlineData("[img=200]https://example.org/p.png[/img]")]
    [InlineData("[img=200x100]https://example.org/p.png[/img]")]
    public void KeepTheSizeFormsWorkingBesideTheAttributeForm(string bbCode)
    {
        var html = ((BbParserWrapper.WrappedNodeTree)_parserProvider
            .GetForSurface(BbSurface.Comment).Parse(bbCode)).ToHtml();

        html.Should().Contain("<img");
        html.Should().Contain("example.org/p.png");
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
        var tree = _parserProvider.GetForSurface(BbSurface.Comment).Parse($"[link=tap]{url}[/link]");

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
        var tree = _parserProvider.GetForSurface(BbSurface.Comment).Parse($"[img]{url}[/img]");

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
        var tree = _parserProvider.GetForSurface(BbSurface.Comment)
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
        var tree = _parserProvider.GetForSurface(BbSurface.Comment)
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
    /// Everything the author-edit rendering emits carries a data-bb-* marker.
    /// </summary>
    /// <remarks>
    /// The reading variant closes the block with a recipients line, which is text
    /// for a reader and nothing the reverse conversion can match: seeded into the
    /// editor it arrived as literal markup the author had to delete by hand, and
    /// saving it stored the markup. The addressees are already in the attribute
    /// the tag is rebuilt from, so the author-edit variant closes on the div.
    /// </remarks>
    [Fact]
    public void CloseThePrivateBlockWithoutTheRecipientsLine_OnAuthorEdit()
    {
        var tree = _parserProvider.GetForAuthorEdit(BbSurface.GamePost)
            .Parse("[private=Вася]тайна[/private]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Be(
            "<div class=\"private-message\" data-bb-tag=\"private\" data-bb-addressees=\"Вася\">тайна</div>");
    }

    /// <summary>
    /// The reader still gets the recipients line: the trade above is the author's
    /// alone, and dropping it everywhere would hide who a private block is for.
    /// </summary>
    [Fact]
    public void KeepTheRecipientsLine_ForAReader()
    {
        var tree = _parserProvider.GetForSurface(BbSurface.GamePost).Parse("[private=Вася]тайна[/private]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain("<div class=\"private-message-header\">Получатели: Вася</div>");
    }

    /// <summary>
    /// The other half of that contract: encoding must not reach what the reader
    /// sees. An ordinary name stays itself, and an ampersand in it arrives as one
    /// character in the browser rather than as an entity on the page.
    /// </summary>
    [Fact]
    public void KeepQuoteAuthor_WhenItIsAnOrdinaryName()
    {
        var tree = _parserProvider.GetForSurface(BbSurface.Comment).Parse("[quote=\"Вася & Петя\"]t[/quote]");

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
        var tree = _parserProvider.GetForSurface(BbSurface.Comment).Parse($"[img]{url}[/img]");

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
        var tree = _parserProvider.GetForSurface(BbSurface.Comment).Parse("[img]\n  https://example.com/a.png\n[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain("<img src=\"https://example.com/a.png\"");
    }

    /// <summary>
    /// The public surfaces put an image behind a spoiler, the rest embed it.
    /// </summary>
    /// <remarks>
    /// The gate used to be declared as a tag: the "safe" tag sets swapped the img
    /// template for one wrapping the picture in a spoiler. This wrapper renders
    /// [img] itself, before the inner parser is ever handed the text, so the
    /// declaration decided nothing and the guest-readable global chat auto-loaded
    /// whatever address a message named. The decision lives on the wrapper now,
    /// and nothing but a test says which surfaces asked for it.
    /// </remarks>
    [Theory]
    [InlineData(BbSurface.GlobalChatMessage, true)]
    [InlineData(BbSurface.GamePost, false)]
    [InlineData(BbSurface.Comment, false)]
    [InlineData(BbSurface.Profile, false)]
    [InlineData(BbSurface.DirectMessage, false)]
    public void GateImages_OnlyWhereTheSurfaceAsksForIt(BbSurface surface, bool gated)
    {
        var tree = _parserProvider.GetForSurface(surface)
            .Parse("[img]https://example.com/a.png[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain("<img ");
        html.Contains("spoiler-head").Should().Be(gated);
    }

    /// <summary>
    /// And the audience that exists to be embeddable safely gets it everywhere.
    /// </summary>
    [Theory]
    [InlineData(BbSurface.GamePost)]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.Profile)]
    public void GateImages_OnEverySafeParser(BbSurface surface)
    {
        var tree = _parserProvider.GetSafeForSurface(surface)
            .Parse("[img]https://example.com/a.png[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain("<img ").And.Contain("spoiler-head");
    }

    /// <summary>
    /// [code] and [noparse] show markup, they do not run it.
    /// </summary>
    /// <remarks>
    /// [img] and [link] are extracted before the parser, which knows nothing of
    /// tags at that point, so a code sample containing one rendered the picture
    /// instead of printing the tag: showing an example of the markup was
    /// impossible, and a moderator quoting somebody's markup re-embedded their
    /// image. [b] inside [noparse] always worked, which is what made the hole
    /// look like it could not exist.
    /// </remarks>
    [Theory]
    [InlineData("[code][img]https://example.com/a.png[/img][/code]")]
    [InlineData("[noparse][img]https://example.com/a.png[/img][/noparse]")]
    [InlineData("[code][link=text]https://example.com[/link][/code]")]
    [InlineData("[noparse][mention=\"Вася\"][/noparse]")]
    public void ShowMarkupInsideVerbatimBlocks_RatherThanRenderIt(string input)
    {
        var tree = _parserProvider.GetForSurface(BbSurface.Comment).Parse(input);

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().NotContain("<img").And.NotContain("<a ");
    }

    /// <summary>
    /// The counterweight: outside the block the same tags still render.
    /// </summary>
    [Fact]
    public void KeepRenderingTheSameTagsOutsideAVerbatimBlock()
    {
        var tree = _parserProvider.GetForSurface(BbSurface.Comment)
            .Parse("[code][img]https://example.com/a.png[/img][/code][img]https://example.com/b.png[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain("<img src=\"https://example.com/b.png\"");
        html.Should().NotContain("<img src=\"https://example.com/a.png\"");
    }

    /// <summary>
    /// An image tag the author never closed costs them the tag, and nothing else.
    /// </summary>
    /// <remarks>
    /// It used to cost them the rest of the paragraph. The content pattern ran to
    /// the next [/img] anywhere in the post, so the unclosed tag joined up with
    /// the closing tag of the following image and everything between the two
    /// became one URL — which then failed the whitespace check and was dropped
    /// whole. The author saw their own text disappear from the page with no
    /// refusal and no message anywhere.
    /// </remarks>
    [Fact]
    public void EatNothingBetweenAnUnclosedImageAndTheNextClosedOne()
    {
        var tree = _parserProvider.GetForSurface(BbSurface.Comment).Parse(
            "начало [img]https://example.com/a.png\nсередина\n[img]https://example.com/b.png[/img] конец");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain("начало").And.Contain("середина").And.Contain("конец");
        // The tag that was never closed stays where it stands, as the text it is,
        // and the one that was closed still renders.
        html.Should().Contain("https://example.com/a.png");
        html.Should().Contain("<img src=\"https://example.com/b.png\"");
    }

    /// <summary>
    /// A bracket that opens no tag is an ordinary URL character.
    /// </summary>
    /// <remarks>
    /// The counterweight to the rule above: narrowed to "up to the next bracket"
    /// it would have cost a literal IPv6 host and an ordinary bracketed query
    /// parameter, both of which are addresses people really write.
    /// </remarks>
    [Theory]
    [InlineData("http://[2001:db8::1]/x.png")]
    [InlineData("https://example.com/x.png?filter[name]=a")]
    public void KeepImage_WhenTheUrlItselfCarriesBrackets(string url)
    {
        var tree = _parserProvider.GetForSurface(BbSurface.Comment).Parse($"[img]{url}[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain("<img src=");
    }

    /// <summary>
    /// No javascript: URL leaves this renderer.
    /// </summary>
    /// <remarks>
    /// A javascript: href is inline script as far as CSP is concerned, and two
    /// decorative ones were the entire reason script-src carried 'unsafe-inline'
    /// on a site that binds server-rendered user HTML through v-html on every
    /// page. The client preventDefaults both toggles, so the href never had a job.
    /// </remarks>
    [Theory]
    [InlineData("[spoiler]hidden[/spoiler]")]
    [InlineData("[nsfw]shocking[/nsfw]")]
    [InlineData("[img]https://example.com/a.png[/img]")]
    public void EmitNoJavascriptUrl(string input)
    {
        var chat = ((BbParserWrapper.WrappedNodeTree)_parserProvider
            .GetForSurface(BbSurface.GlobalChatMessage).Parse(input)).ToHtml();
        var common = ((BbParserWrapper.WrappedNodeTree)_parserProvider
            .GetForSurface(BbSurface.Comment).Parse(input)).ToHtml();

        chat.Should().NotContain("javascript:");
        common.Should().NotContain("javascript:");
    }
}
