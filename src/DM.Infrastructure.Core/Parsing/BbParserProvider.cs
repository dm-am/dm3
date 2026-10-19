using System;
using System.Collections.Generic;
using BBCodeParser;
using BBCodeParser.Tags;
using DM.Infrastructure.Core.Parsing.Tags;

namespace DM.Infrastructure.Core.Parsing;

/// <inheritdoc />
/// <remarks>
/// The tag catalogue: which tags each surface reads and what each renders to.
/// The grammar is the parser's (docs/architecture/BBCODE_RENDERING/grammar.md).
/// </remarks>
public class BbParserProvider : IBbParserProvider
{
    private const string CodeClassName = "code";
    private const string QuoteClassName = "quote";
    private const string PrivateClassName = "private-message";

    private static readonly Tag Strong = new("b", "<strong>", "</strong>");
    private static readonly Tag Italic = new("i", "<em>", "</em>");
    private static readonly Tag Underlined = new("u", "<u>", "</u>");
    private static readonly Tag Strike = new("strike", "<s>", "</s>");

    // The reading blocks drop the line break typed after them (Tag.DropsLineBreakAfter).

    // href="#" for the reason SpoilerTag gives.
    private static readonly Tag Nsfw = new("nsfw",
        "<a href=\"#\" class=\"nsfw-head\" data-swaptext=\"Скрыть шокирующий контент\">Показать шокирующий контент</a><div class=\"nsfw-spoiler\">",
        "</div>")
    {
        DropsLineBreakAfter = true
    };

    private static readonly Tag Mod = new("mod", "<div class=\"mod-block\">", "</div>") { DropsLineBreakAfter = true };

    private static readonly ListTag OrderedList = new("ol", "<ol>", "</ol>") { DropsLineBreakAfter = true };
    private static readonly ListTag UnorderedList = new("ul", "<ul>", "</ul>") { DropsLineBreakAfter = true };
    private static readonly Tag ListItem = new ListItemTag { DropsLineBreakAfter = true };

    private static readonly Tag Spoiler = new SpoilerTag();

    private static readonly Tag Quote = new("quote", $"<div class=\"{QuoteClassName}\">", "</div>")
    {
        DropsLineBreakAfter = true
    };

    private static readonly Tag Tab = new("tab", "&nbsp;&nbsp;&nbsp;");

    // [code] as on DM2: a monospace font in the line, the markup inside it works as
    // anywhere.
    private static readonly Tag Code = new("code", $"<code class=\"{CodeClassName}\">", "</code>");

    // [noparse] shows its content as typed: the way to show markup as an example.
    private static readonly CodeTag Noparse = new("noparse", "", "");

    // What makes an address safe to put on a page is in BbAddress.
    private static readonly Tag Image = new ImageTag(ImageGate.None);
    private static readonly Tag ChatImage = new ImageTag(ImageGate.ReaderChoice);
    private static readonly Tag SafeImage = new ImageTag(ImageGate.Always);
    private static readonly Tag Link = new LinkTag();
    private static readonly Tag Mention = new MentionTag();

    // Sealed (Tag.SealedByOwnTag) and hiding (Tag.Hides). Read on every surface so
    // that text stored before the save path refused it is erased, not printed;
    // shown on the game post alone. [mod] is not sealed, being public on read.
    private static readonly Tag Private = new PrivateTag(
        $"<span class=\"{PrivateClassName}\" data-recipients=\"{{value}}\">", "</span>");

    // CR LF is one break, so [code] stored from a DM2 form draws no empty lines.
    // The order matters: CR LF first.
    private static readonly Dictionary<string, string> CommonSubstitutions = new()
    {
        {"---", "&mdash;"},
        {"--", "&ndash;"},
        {"\r\n", "<br />"},
        {"\n", "<br />"}
    };

    // The same typography in plain text, where an entity is not markup.
    private static readonly Dictionary<string, string> CommonTextSubstitutions = new()
    {
        {"---", "\u2014"},
        {"--", "\u2013"}
    };

    private static TagSetBuilder DefaultTags(Tag image) => new([
        Strong, Italic, Underlined, Strike,
        Spoiler, Quote,
        UnorderedList, OrderedList, ListItem,
        Tab, Code, Noparse, Nsfw,
        image, Link, Mention,
        Private
    ]);

    private static BbParserWrapper Display(TagSetBuilder tags) =>
        new(new BbParser(tags.Build(), BbParser.SecuritySubstitutions, CommonSubstitutions,
            CommonTextSubstitutions));

    private static readonly Lazy<BbParserWrapper> CommentParser = new(() => Display(DefaultTags(Image).With(Mod)));

    // The game post, the profile and the direct message, told apart by the filter. No
    // [mod]: in a private chat it would let a user impersonate a moderator.
    private static readonly Lazy<BbParserWrapper> DefaultParser = new(() => Display(DefaultTags(Image)));

    private static readonly Lazy<BbParserWrapper> GlobalChatMessageParser = new(() =>
        Display(DefaultTags(ChatImage).With(Mod)));

    // Images always behind a spoiler; the filter cuts [private] out for everybody.
    private static readonly Lazy<BbParserWrapper> SafeParser = new(() => Display(DefaultTags(SafeImage)));

    // The hint (BbSurface.Hint): [tip], with [noparse] and [private] as on every
    // surface. A line break is drawn, nothing else is substituted.

    private static readonly Tag Tip = new TipTag();

    private static readonly Dictionary<string, string> HintSubstitutions = new()
    {
        {"\r\n", "<br />"},
        {"\n", "<br />"}
    };

    private static readonly Lazy<BbParserWrapper> HintParser = new(() =>
        new BbParserWrapper(new BbParser(new TagSetBuilder([Tip, Noparse, Private]).Build(),
            BbParser.SecuritySubstitutions, HintSubstitutions)));

    /// <inheritdoc />
    public BbParserWrapper GetForSurface(BbSurface surface) => surface switch
    {
        BbSurface.GamePost or BbSurface.Profile or BbSurface.DirectMessage => DefaultParser.Value,
        BbSurface.Comment => CommentParser.Value,
        BbSurface.GlobalChatMessage => GlobalChatMessageParser.Value,
        BbSurface.Hint => HintParser.Value
    };

    /// <inheritdoc />
    /// <remarks>
    /// The hint has no safe variant: it shows no image, and the safe set would read
    /// the tags of a post in it.
    /// </remarks>
    public BbParserWrapper GetSafeForSurface(BbSurface surface) =>
        surface == BbSurface.Hint ? HintParser.Value : SafeParser.Value;
}
