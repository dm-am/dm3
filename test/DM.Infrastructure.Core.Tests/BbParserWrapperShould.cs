using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

public class BbParserWrapperShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private static readonly TestSubject Author = new()
    {
        UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Role = UserRole.RegularUser,
        IsAuthenticated = true,
        AccessPolicy = AccessPolicy.NotSpecified
    };

    /// <summary>What the author of the text reads, hidden blocks included.</summary>
    private string Html(BbSurface surface, string bbCode) =>
        ((BbParserWrapper)_parserProvider.GetForSurface(surface)).RenderHtml(bbCode, new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = surface,
            Viewer = Author,
            PostAuthorUserId = Author.UserId
        });

    /// <summary>What the author's own editor is given.</summary>
    /// <summary>
    /// The markup the author's editor is given, less the marker every piece of
    /// it starts with (BbParserWrapper.LineBreaksWritten), which is checked here.
    /// </summary>
    private string AuthorEdit(BbSurface surface, string bbCode)
    {
        var html = ((BbParserWrapper)_parserProvider.GetForAuthorEdit(surface))
            .RenderHtml(bbCode, RenderContext.ForAuthorEdit(Author, surface));

        html.Should().StartWith(BbParserWrapper.LineBreaksWritten,
            "the editor reads the author's markup by its own rule for line breaks");
        return html[BbParserWrapper.LineBreaksWritten.Length..];
    }

    /// <summary>The rendering that is safe to embed anywhere.</summary>
    private string EmbedSafe(BbSurface surface, string bbCode) =>
        ((BbParserWrapper)_parserProvider.GetSafeForSurface(surface))
        .RenderHtml(bbCode, RenderContext.ForEmbedSafe(surface));

    private string Comment(string bbCode) => Html(BbSurface.Comment, bbCode);

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
        Comment("[img]https://example.com/image.png[/img]").Should().Contain(attribute);
    }

    /// <summary>
    /// A line break typed as CR LF is one break on the page, in [code] as well.
    /// </summary>
    /// <remarks>
    /// [code] keeps its line breaks (white-space: pre-wrap), and the browser
    /// turns the CR it parses into LF, so "\r&lt;br /&gt;" drew an empty line
    /// under every line of a text stored with CR LF, the way a DM2 form sent it.
    /// </remarks>
    [Theory]
    [InlineData("[code]раз\r\nдва[/code]", "[code]раз\nдва[/code]")]
    [InlineData("раз\r\nдва", "раз\nдва")]
    public void DrawOneBreakForALineBreakTypedAsCrLf(string typed, string withLineFeed) =>
        Comment(typed).Should().Be(Comment(withLineFeed));

    [Fact]
    public void PreserveImgAndLinkTags_ToHtml()
    {
        var result = Comment("[img]https://example.com/image.png[/img]\n\n[link]https://example.com[/link]");

        result.Should().Contain("<img src=\"https://example.com/image.png\"");
        result.Should().Contain("<a href=\"https://example.com\"");
        result.Should().Contain(">ссылка</a>");
    }

    [Fact]
    public void ShowTheTextOfALink_WhenItWasGivenOne()
    {
        var result = Comment("[link text=\"Click here\"]https://example.com[/link]");

        result.Should().Be("<a href=\"https://example.com\" target=\"_blank\" rel=\"noopener\">Click here</a>");
    }

    [Fact]
    public void ConvertSimpleSpoiler_ToHtml_UsesDefaultText()
    {
        var result = Comment("[spoiler]hidden content[/spoiler]");

        result.Should().Contain("class=\"spoiler-head\"");
        result.Should().Contain(">Показать содержимое</a>");
        result.Should().Contain("hidden content");
    }

    /// <summary>
    /// The caption is author text written into element text, so it is encoded
    /// there; it used to go through the tag's "secure" filter, which deleted its
    /// spaces and quotes instead.
    /// </summary>
    [Fact]
    public void EncodeTheCaptionOfASpoiler_AndKeepItsSpaces()
    {
        var result = Comment("[spoiler text=\"Карта <b>мира</b> и \"еще\"\"]x[/spoiler]");

        result.Should().Contain(">Карта &lt;b&gt;мира&lt;/b&gt; и &quot;еще&quot;</a>");
        result.Should().NotContain("<b>");
    }

    /// <summary>
    /// A [link] whose URL is rejected is printed as it was typed, and what was
    /// typed is attacker-controlled: it is encoded like any other text. Emitting
    /// it raw is stored XSS on every content surface (comments, posts, profiles,
    /// DMs).
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHN2Zz4=")]
    [InlineData("vbscript:msgbox(1)")]
    public void EncodeLinkText_WhenUrlIsRejected(string dangerousUrl)
    {
        var result = Comment($"[link text=\"<img src=x onerror=alert(1)>\"]{dangerousUrl}[/link]");

        result.Should().NotContain("<img src=x onerror=alert(1)>");
        result.Should().Be($"[link text=\"&lt;img src=x onerror=alert(1)&gt;\"]{dangerousUrl}[/link]");
    }

    /// <summary>
    /// Same guarantee for the accepted-URL branch, so the two paths cannot
    /// drift apart again.
    /// </summary>
    [Fact]
    public void EncodeLinkText_WhenUrlIsAccepted()
    {
        var result = Comment("[link text=\"<b>bold</b>\"]https://example.com[/link]");

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
    public void PrintTheImageTagAsTyped_WhenUrlPointsIntoReaderNetwork(string url)
    {
        Comment($"[img]{url}[/img]").Should().Be($"[img]{url}[/img]");
    }

    /// <summary>
    /// The attribute spellings of the tag are no tag at all.
    /// </summary>
    /// <remarks>
    /// [img="URL"] and [img=URL] used to be read as images, first by nobody —
    /// the parser substituted the value into a template, skipping every guard on
    /// the address — and later by an extraction of their own. Neither DM2 nor the
    /// grammar has such a form, so it is a broken tag and shows as it was typed,
    /// which is both safe and honest.
    /// </remarks>
    [Theory]
    [InlineData("http://127.0.0.1/x.png")]
    [InlineData("http://169.254.169.254/x.png")]
    [InlineData("https://example.org/picture.png")]
    [InlineData("ftp://evil.example/x.png")]
    public void ShowTheAttributeFormAsText(string url)
    {
        Comment($"[img=\"{url}\"]").Should().Be($"[img=\"{url}\"]");
        Comment($"[img={url}]").Should().Be($"[img={url}]");
    }

    /// <summary>A size is a parameter, and one side is enough.</summary>
    [Theory]
    [InlineData("[img width=\"200\"]https://example.org/p.png[/img]", "data-bb-width=\"200\"")]
    [InlineData("[img height=\"100\"]https://example.org/p.png[/img]", "data-bb-height=\"100\"")]
    [InlineData("[img width=\"200\" height=\"100\"]https://example.org/p.png[/img]",
        "style=\"--bb-image-max-width:200px;--bb-image-max-height:100px\"")]
    public void SizeTheImage_ByItsParameters(string bbCode, string expected)
    {
        var html = Comment(bbCode);

        html.Should().Contain("<img").And.Contain("example.org/p.png").And.Contain(expected);
    }

    /// <summary>
    /// The same guard on the [link] path: a rejected URL costs the anchor, and
    /// the tag is printed as it was typed, so the author and the reader both see
    /// what did not work.
    /// </summary>
    [Theory]
    [InlineData("http://[::ffff:127.0.0.1]/probe")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    public void PrintTheLinkTagAsTyped_WhenLinkPointsIntoReaderNetwork(string url)
    {
        var html = Comment($"[link text=\"tap\"]{url}[/link]");

        html.Should().NotContain("<a href");
        html.Should().Be($"[link text=\"tap\"]{url}[/link]");
    }

    /// <summary>
    /// The plain text of a refused image or link is the tag as typed as well:
    /// the text channels show what the page shows.
    /// </summary>
    [Theory]
    [InlineData("[img]javascript:alert(1)[/img]")]
    [InlineData("[link text=\"tap\"]http://127.0.0.1/[/link]")]
    public void PrintARefusedAddressAsTyped_InThePlainTextToo(string bbCode)
    {
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment);

        wrapper.RenderText(bbCode, RenderContext.ForPlainText() with { Surface = BbSurface.Comment })
            .Should().Be(bbCode);
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
        Comment($"[img]{url}[/img]").Should().Contain($"<img src=\"{url}\"");
    }

    /// <summary>
    /// Text that looks like an old placeholder is text. The images used to be cut
    /// out of the post and stood in for by a placeholder that ordinary text could
    /// spell, and a post that carried those characters next to an image got a
    /// second copy of the image pasted at that spot.
    /// </summary>
    [Theory]
    [InlineData("__IMG_0__")]
    [InlineData("I0")]
    public void RenderAnImage_OnlyWhereItsTagStood(string typedByAuthor)
    {
        var html = Comment($"{typedByAuthor} [img]https://example.com/a.png[/img]");

        (html.Split("<img ").Length - 1).Should().Be(1);
    }

    /// <summary>
    /// The same holds where text can never become an element: the recipients a
    /// private block carries are built from a tag attribute.
    /// </summary>
    [Fact]
    public void KeepTheAttributeAsText_WhenItLooksLikeAnImage()
    {
        var html = Html(BbSurface.GamePost,
            "[private=\"__IMG_0__\"]text[/private][img]https://example.com/a.png[/img]");

        html.Should().Contain(
            "<span class=\"private-message\" data-recipients=\"DungeonMaster, Assistant, __IMG_0__\">text</span>");
    }

    /// <summary>
    /// A tag attribute is author text, so it is encoded where the tag writes it.
    /// </summary>
    [Fact]
    public void EncodePrivateAddressee_InTheRecipients()
    {
        var html = Html(BbSurface.GamePost, "[private=\"<img src=x onerror=alert(1)>\"]t[/private]");

        html.Should().NotContain("<img src=x onerror=alert(1)>");
        html.Should().Contain(
            "data-recipients=\"DungeonMaster, Assistant, &lt;img src=x onerror=alert(1)&gt;\">t</span>");
    }

    /// <summary>
    /// The author-edit variant puts the same value into an attribute of its own,
    /// where an unencoded quote closes that attribute and everything after it is
    /// read as attributes of the span — an event handler among them. The value runs
    /// to the first quote followed by the closing bracket, so it can carry quotes.
    /// </summary>
    [Fact]
    public void EncodePrivateAddressee_OnAuthorEditAttribute()
    {
        var html = AuthorEdit(BbSurface.GamePost, "[private=\"a\"onmouseover=alert(1) x=\"\"]s[/private]");

        html.Should().Contain("data-bb-addressees=\"a&quot;onmouseover=alert(1) x=&quot;\"");
    }

    /// <summary>
    /// Everything the author-edit rendering emits carries a data-bb-* marker.
    /// </summary>
    /// <remarks>
    /// The reading variant carries the recipients, which are for a reader and
    /// nothing the reverse conversion reads; when they were a line after the
    /// block, seeded into the editor they arrived as literal markup the author
    /// had to delete by hand, and saving it stored the markup. The addressees are
    /// already in the attribute the tag is rebuilt from, so the author-edit
    /// variant carries no recipients at all.
    /// </remarks>
    [Fact]
    public void CloseThePrivateBlockWithoutTheRecipients_OnAuthorEdit()
    {
        var html = AuthorEdit(BbSurface.GamePost, "[private=\"Вася\"]тайна[/private]");

        html.Should().Be(
            "<span class=\"private-message\" data-bb-tag=\"private\" data-bb-addressees=\"Вася\">тайна</span>");
    }

    /// <summary>
    /// The author of a block the grammar did not accept gets it back as it was
    /// typed: the save path refuses it and names the fix, and a block rebuilt in
    /// the right shape would hide the spelling the refusal talks about.
    /// </summary>
    [Theory]
    [InlineData("[private=Вася]тайна[/private]")]
    [InlineData("[private to=\"Вася\"]тайна[/private]")]
    [InlineData("[private=\"Вася\"]тайна")]
    public void GiveTheAuthorAMalformedBlockAsItWasTyped(string bbCode)
    {
        var html = AuthorEdit(BbSurface.GamePost, bbCode);

        html.Should().Be(bbCode);
    }

    /// <summary>
    /// A name in capitals is the same name, so the block is the block and the
    /// author's editor gets it as one. The markup remembers no case, and the
    /// editor writes the tag back in lower case.
    /// </summary>
    [Theory]
    [InlineData("[PRIVATE]тайна[/PRIVATE]", "")]
    [InlineData("[Private=\"Вася\"]тайна[/private]", "Вася")]
    public void GiveTheAuthorABlockInCapitalsAsABlock(string bbCode, string addressees)
    {
        AuthorEdit(BbSurface.GamePost, bbCode).Should().Be(
            $"<span class=\"private-message\" data-bb-tag=\"private\" data-bb-addressees=\"{addressees}\">тайна</span>");
    }

    /// <summary>
    /// Hiding markup where it does not work is erased for every reader, the
    /// author included, and handed to the author's editor as the text it is.
    /// </summary>
    /// <remarks>
    /// The save path refuses such text, so what reaches the page never went
    /// through it: stored before the refusal existed, or carried over from DM2.
    /// Shown as text it printed the secret to everybody. The author gets it back
    /// in the editor, sees it, and hears from the save path why it cannot stay.
    /// </remarks>
    [Theory]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.GlobalChatMessage)]
    [InlineData(BbSurface.Profile)]
    [InlineData(BbSurface.DirectMessage)]
    public void EraseHidingMarkupWhereItDoesNotWork_AndGiveItToTheAuthorAsText(BbSurface surface)
    {
        const string bbCode = "до [private=\"Вася\"]тайна[/private] после";

        Html(surface, bbCode).Should().Be("до  после");
        AuthorEdit(surface, bbCode).Should().Be(bbCode);
        EmbedSafe(surface, bbCode).Should().Be("до  после");
    }

    /// <summary>
    /// The reader still gets the recipients, for the tooltip over the block: the
    /// trade above is the author's alone, and dropping them everywhere would hide
    /// who a private block is for. They are no text of the line (the owner,
    /// 05.10: the tooltip says it), so the block is the green words and nothing
    /// after them.
    /// </summary>
    [Fact]
    public void GiveAReaderTheRecipientsOnTheBlock_AndNoLineAfterIt()
    {
        Html(BbSurface.GamePost, "до [private=\"Вася\"]тайна[/private] после")
            .Should().Be(
                "до <span class=\"private-message\" data-recipients=\"DungeonMaster, Assistant, Вася\">тайна</span> после");
    }

    /// <summary>
    /// The master and the assistants read every hidden block of the game, so the
    /// recipients name them first, whoever the block is for, and the names of the
    /// block after them. A name the author wrote for a lead is not named a second
    /// time, in whatever case it was written; the names are the list the save
    /// path reads, each without the space around it and an empty one left out.
    /// </summary>
    [Theory]
    [InlineData("[private=\"Лиара Тенебраум\"]тайна[/private]", "DungeonMaster, Assistant, Лиара Тенебраум")]
    [InlineData("[private=\"Assistant, Вася, dungeonmaster\"]тайна[/private]", "DungeonMaster, Assistant, Вася")]
    [InlineData("[private=\"DungeonMaster\"]тайна[/private]", "DungeonMaster, Assistant")]
    [InlineData("[private=\"Вася ,  Петя,\"]тайна[/private]", "DungeonMaster, Assistant, Вася, Петя")]
    public void NameTheLeadsFirst_ThenTheNamesOfTheBlock(string bbCode, string recipients)
    {
        Html(BbSurface.GamePost, bbCode)
            .Should().Contain($"<span class=\"private-message\" data-recipients=\"{recipients}\">");
    }

    /// <summary>
    /// A block that names nobody is read by the master and the assistants, and
    /// its recipients name them as DM2 posts do, in Latin letters: the line it
    /// stood on used to end on an empty "Получатели:". The terms are plain text,
    /// as any name is: drawn bold and in a colour of their own, they read as a
    /// different thing from the names of the next block. An empty value and a
    /// value of commas alone name nobody, as a bare block does, and so does a
    /// spelling the grammar did not accept.
    /// </summary>
    [Theory]
    [InlineData("[private]тайна[/private]")]
    [InlineData("[private=\"\"]тайна[/private]")]
    [InlineData("[private=\" , \"]тайна[/private]")]
    [InlineData("[private=Вася]тайна[/private]")]
    public void NameTheLeadsInTheRecipients_OfABlockThatNamesNobody(string bbCode)
    {
        Html(BbSurface.GamePost, bbCode)
            .Should().Contain("<span class=\"private-message\" data-recipients=\"DungeonMaster, Assistant\">");
    }

    /// <summary>
    /// The other half of that contract: encoding must not reach what the reader
    /// sees. An ordinary name stays itself, and an ampersand in it arrives as one
    /// character in the browser rather than as an entity on the page.
    /// </summary>
    [Fact]
    public void KeepPrivateAddressee_WhenItIsAnOrdinaryName()
    {
        Html(BbSurface.GamePost, "[private=\"Вася & Петя\"]t[/private]")
            .Should().Contain("data-recipients=\"DungeonMaster, Assistant, Вася &amp; Петя\"");
    }

    /// <summary>
    /// A URI carries no raw whitespace, and one that does is what turns a quoted
    /// attribute into two: the second row is the payload that becomes an event
    /// handler on the element the image ends up nested inside. Such content is
    /// not one address, so the tag shows as the text it is.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/a b.png")]
    [InlineData("https://example.com/a.png onmouseover=alert(1) x=")]
    [InlineData("https://example.com/a\tb.png")]
    [InlineData("https://example.com/a\nb.png")]
    public void DropImage_WhenUrlCarriesWhitespace(string url)
    {
        Comment($"[img]{url}[/img]").Should().NotContain("<img");
    }

    /// <summary>
    /// And its counterweight: the newlines an editor leaves around a pasted
    /// address are still trimmed, so the rule above costs no working image.
    /// </summary>
    [Fact]
    public void KeepImage_WhenUrlIsPaddedWithNewlines()
    {
        Comment("[img]\n  https://example.com/a.png\n[/img]").Should().Contain("<img src=\"https://example.com/a.png\"");
    }

    /// <summary>
    /// The global chat puts an image behind a spoiler marked as the gate, which
    /// the client opens in the full layout and keeps closed in the compact one;
    /// the rest embed it.
    /// </summary>
    /// <remarks>
    /// The gate used to be declared as a tag: the "safe" tag sets swapped the img
    /// template for one wrapping the picture in a spoiler, while [img] was cut out
    /// of the text before the parser saw it, so the declaration decided nothing
    /// and the guest-readable global chat auto-loaded whatever address a message
    /// named. The image tag of each rendering decides it now.
    /// </remarks>
    [Theory]
    [InlineData(BbSurface.GlobalChatMessage, true)]
    [InlineData(BbSurface.GamePost, false)]
    [InlineData(BbSurface.Comment, false)]
    [InlineData(BbSurface.Profile, false)]
    [InlineData(BbSurface.DirectMessage, false)]
    public void GateImages_OnlyWhereTheSurfaceAsksForIt(BbSurface surface, bool gated)
    {
        var html = Html(surface, "[img]https://example.com/a.png[/img]");

        html.Should().Contain("<img ");
        html.Contains("spoiler-head").Should().Be(gated);
        html.Contains("data-bb-gate=\"image\"").Should().Be(gated);
    }

    /// <summary>
    /// A message written on two lines is shown on two lines, private messages
    /// included.
    /// </summary>
    /// <remarks>
    /// The page puts the rendered text into a block that collapses white space,
    /// so the break is the renderer's to draw. The private-message surface kept
    /// it as typed, and a reader got the two lines run together with a space.
    /// </remarks>
    [Theory]
    [InlineData(BbSurface.DirectMessage)]
    [InlineData(BbSurface.GamePost)]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.GlobalChatMessage)]
    [InlineData(BbSurface.Profile)]
    public void DrawTheLineBreakOnEverySurface(BbSurface surface)
    {
        Html(surface, "раз\nдва").Should().Be("раз<br />два");
    }

    /// <summary>
    /// The author's editor reads the break back from the same markup on every
    /// surface, so a message edited in place keeps its lines.
    /// </summary>
    [Fact]
    public void GiveThePrivateMessageAuthorTheBreakTheEditorReadsBack()
    {
        AuthorEdit(BbSurface.DirectMessage, "раз\nдва").Should().Be("раз<br />два");
    }

    /// <summary>
    /// The gate the author wrote is not the reader's to open: a spoiler around an
    /// image in the chat carries no mark, and the image inside it carries its own.
    /// </summary>
    [Fact]
    public void MarkOnlyTheGateTheServerAdded()
    {
        var html = Html(BbSurface.GlobalChatMessage, "[spoiler][img]https://example.com/a.png[/img][/spoiler]");

        html.Should().StartWith(
            "<a href=\"#\" class=\"spoiler-head\" data-swaptext=\"Скрыть содержимое\">Показать содержимое</a>" +
            "<div class=\"spoiler\"><a href=\"#\" class=\"spoiler-head\" data-bb-gate=\"image\">");
    }

    /// <summary>
    /// The author's own editor gets no gate at all.
    /// </summary>
    [Fact]
    public void LeaveTheGateOutOfTheAuthorsEditor()
    {
        var html = AuthorEdit(BbSurface.GlobalChatMessage, "[img]https://example.com/a.png[/img]");

        html.Should().StartWith("<img ").And.NotContain("spoiler");
    }

    /// <summary>
    /// And the audience that exists to be embeddable safely gets it everywhere,
    /// with no mark: it has no layout to ask.
    /// </summary>
    [Theory]
    [InlineData(BbSurface.GamePost)]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.Profile)]
    public void GateImages_OnEverySafeParser(BbSurface surface)
    {
        var html = EmbedSafe(surface, "[img]https://example.com/a.png[/img]");

        html.Should().Contain("<img ").And.Contain("spoiler-head").And.NotContain("data-bb-gate");
    }

    /// <summary>
    /// [noparse] shows markup, it does not run it. [code] is no such block: it
    /// changes the font, and what the grammar table says of it is the rule.
    /// </summary>
    [Theory]
    [InlineData("[noparse][img]https://example.com/a.png[/img][/noparse]")]
    [InlineData("[noparse][link text=\"text\"]https://example.com[/link][/noparse]")]
    [InlineData("[noparse][mention]Вася[/mention][/noparse]")]
    public void ShowMarkupInsideVerbatimBlocks_RatherThanRenderIt(string input)
    {
        Comment(input).Should().NotContain("<img").And.NotContain("<a ");
    }

    /// <summary>
    /// The counterweight: outside the block the same tags still render.
    /// </summary>
    [Fact]
    public void KeepRenderingTheSameTagsOutsideAVerbatimBlock()
    {
        var html = Comment(
            "[noparse][img]https://example.com/a.png[/img][/noparse][img]https://example.com/b.png[/img]");

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
        var html = Comment(
            "начало [img]https://example.com/a.png\nсередина\n[img]https://example.com/b.png[/img] конец");

        html.Should().Contain("начало").And.Contain("середина").And.Contain("конец");
        // The tag that was never closed stays where it stands, as the text it is,
        // and the one that was closed still renders.
        html.Should().Contain("[img]https://example.com/a.png");
        html.Should().Contain("<img src=\"https://example.com/b.png\"");
    }

    /// <summary>
    /// A bracket that opens no tag is an ordinary URL character.
    /// </summary>
    [Theory]
    [InlineData("http://[2001:db8::1]/x.png")]
    [InlineData("https://example.com/x.png?filter[name]=a")]
    public void KeepImage_WhenTheUrlItselfCarriesBrackets(string url)
    {
        Comment($"[img]{url}[/img]").Should().Contain("<img src=");
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
        Html(BbSurface.GlobalChatMessage, input).Should().NotContain("javascript:");
        Comment(input).Should().NotContain("javascript:");
    }
}
