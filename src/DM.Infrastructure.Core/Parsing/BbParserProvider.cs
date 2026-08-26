using System;
using System.Collections.Generic;
using BBCodeParser;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing;

/// <inheritdoc />
public class BbParserProvider : IBbParserProvider
{

    private const string CodeClassName = "code";
    private const string SpoilerHeadClassName = "spoiler-head";
    private const string SpoilerClassName = "spoiler";
    private const string QuoteClassName = "quote";
    private const string QuoteHeaderClassName = "quote-author";
    private const string PrivateClassName = "private-message";
    private const string PrivateHeaderClassName = "private-message-header";

    private static readonly Tag Strong = new("b", "<strong>", "</strong>");
    private static readonly Tag Italic = new("i", "<em>", "</em>");
    private static readonly Tag Underlined = new("u", "<u>", "</u>");
    private static readonly Tag Strike = new("strike", "<s>", "</s>");

    // NSFW - same structure as spoiler, but with nsfw-head toggle and nsfw-spoiler content
    // href="#" and not javascript:void(0): a javascript: URL is inline script to
    // CSP, and two decorative ones were the whole reason script-src carried
    // 'unsafe-inline' on a site that binds user HTML through v-html. The client
    // preventDefaults the click on both heads, so the href never navigates.
    private static readonly Tag Nsfw = new("nsfw",
        "<a href=\"#\" class=\"nsfw-head\" data-swaptext=\"Скрыть шокирующий контент\">Показать шокирующий контент</a><div class=\"nsfw-spoiler\">",
        "</div>");

    // Mod block - green highlighted block for moderator messages (Common and Message contexts only)
    private static readonly Tag Mod = new("mod", "<div class=\"mod-block\">", "</div>");

    // AuthorEdit variant of [mod] — emits data-bb-tag so Tiptap's ModBlock
    // extension can round-trip the tag back into BBCode on save.
    private static readonly Tag ModAuthorEdit = new(
        "mod", "<div class=\"mod-block\" data-bb-tag=\"mod\">", "</div>");
    private static readonly ListTag OrderedList = new("ol", "<ol>", "</ol>");
    private static readonly ListTag UnorderedList = new("ul", "<ul>", "</ul>");
    private static readonly Tag ListItem = new("li", "<li>", "</li>");

    // Spoiler — same structure as NSFW, and the same href="#" for the same CSP
    // reason: the client preventDefaults the click, so the href never navigates.
    private static readonly Tag Spoiler = new("spoiler",
        $"<a href=\"#\" class=\"{SpoilerHeadClassName}\" data-swaptext=\"Скрыть содержимое\">Показать содержимое</a><div class=\"{SpoilerClassName}\">",
        "</div>");

    private static readonly Tag Quote = new("quote",
        $"<div class=\"{QuoteClassName}\"><div class=\"{QuoteHeaderClassName}\">{{value}}</div>", "</div>", true,
        false);

    // [img] and [link] are deliberately absent from every tag set below.
    //
    // BbParserWrapper renders both itself, from text it cuts out before the
    // parser ever runs, and everything that makes a URL safe to put on a page
    // lives there: the scheme white list, the refusal of loopback and private
    // addresses, the spoiler gate on public surfaces, referrerpolicy, lazy
    // loading. Leaving the tags in the parser's set as well meant any spelling
    // the wrapper's patterns did not catch fell through to a template that
    // substitutes the address as written — and the spellings it does not catch
    // are the unclosed ones, which is ordinary mistyping:
    //
    //     [link=vbscript:msgbox(1)]click          -> live anchor, dangerous scheme
    //     [link=http://169.254.169.254/...]meta   -> anchor into the reader's network
    //     [img=200]                               -> <img src="200">
    //
    // Without them in the set an unextracted spelling is not a tag at all, so it
    // renders as the text the author typed. That is both safe and honest, and it
    // is the same answer for every future spelling nobody has thought of.
    private static readonly Tag Tab = new("tab", "&nbsp;&nbsp;&nbsp;");

    private static readonly CodeTag Code = new("code", $"<pre class=\"{CodeClassName}\">", "</pre>");

    // Noparse - outputs content as-is without parsing inner BBCode tags
    private static readonly CodeTag Noparse = new("noparse", "", "");

    // Sealed: only [/private] ends it. A closing tag of some other name that
    // stands inside the block used to end it early, and the rest of the block -
    // written by its author as private text - was printed to the whole room.
    // See Tag.SealedByOwnTag for the worked example; [mod] is deliberately not
    // sealed, being public on read, so ending it early costs a reader nothing
    // but formatting.
    private static readonly Tag Private = new("private", $"<div class=\"{PrivateClassName}\">",
        $"</div><div class=\"{PrivateHeaderClassName}\">Получатели: {{value}}</div>", true, false,
        sealedByOwnTag: true);

    // AuthorEdit variant of [private] — opens with data-bb-tag and
    // data-bb-addressees carrying the raw attribute value so Tiptap's
    // Private extension can round-trip the tag on save.
    //
    // It closes on the div and stops there, unlike the reading variant above. The
    // recipients line is for a reader: it carries no data-bb-* marker, so the
    // reverse conversion has nothing to match and would leave the literal markup
    // sitting in the author's editor — and the same names are already in
    // data-bb-addressees, which is what the tag is rebuilt from.
    private static readonly Tag PrivateAuthorEdit = new(
        "private",
        $"<div class=\"{PrivateClassName}\" data-bb-tag=\"private\" data-bb-addressees=\"{{value}}\">",
        "</div>",
        true, false,
        sealedByOwnTag: true);

    private static readonly Dictionary<string, string> CommonSubstitutions = new()
    {
        {"---", "&mdash;"},
        {"--", "&ndash;"},
        {"\n", "<br />"}
    };

    private static readonly Dictionary<string, string> ChatMessageSubstitutions =
        new()
        {
            {"---", "&mdash;"},
            {"--", "&ndash;"},
        };

    private static readonly Dictionary<string, string> InfoSubstitutions = new()
    {
        {"---", "&mdash;"},
        {"--", "&ndash;"},
        {"\n___", "<hr />"},
        {"\n", "<br />"},
    };

    private static readonly Dictionary<string, string> SafeSubstitutions = new()
    {
        {"---", "&mdash;"},
        {"--", "&ndash;"},
        {"\n", "<br />"}
    };

    // Base tags available in all contexts
    private static TagSetBuilder DefaultTags => new(new[]
    {
        Strong, Italic, Underlined, Strike,
        Spoiler, Quote,
        UnorderedList, OrderedList, ListItem,
        Tab, Code, Noparse, Nsfw
    });

    // "Safe" is one thing: images behind a spoiler. It is not expressible as a
    // tag set but as a wrapper flag, because [img] is extracted before the inner
    // parser sees the text, so a tag set that swapped the img template changed
    // nothing at all. The safe parsers below therefore take the same tag sets as
    // their ordinary counterparts and differ only by spoilerGatedImages.

    // Common context: base tags + mod
    private static readonly Lazy<IBbParser> CommonParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.With(Mod).Build(),
            BbParser.SecuritySubstitutions, CommonSubstitutions)));

    // Post context: base tags + private (no mod)
    private static readonly Lazy<IBbParser> PostParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.With(Private).Build(),
            BbParser.SecuritySubstitutions, CommonSubstitutions)));

    // Info context: base tags (no mod, no private). It differs from the chat
    // message context by its substitutions, not by its tag set: "\n___" becomes
    // a horizontal rule here and nowhere else.
    private static readonly Lazy<IBbParser> InfoParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.Build(),
            BbParser.SecuritySubstitutions, InfoSubstitutions)));

    // Direct/group message context: base tags only — no [mod] (private
    // chats have no moderation, so [mod] would let a user impersonate a
    // moderator) and no [private].
    private static readonly Lazy<IBbParser> ChatMessageParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.Build(),
            BbParser.SecuritySubstitutions, ChatMessageSubstitutions)));

    // General chat context: base tags + mod, with images behind a spoiler
    private static readonly Lazy<IBbParser> GeneralChatMessageParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.With(Mod).Build(),
            BbParser.SecuritySubstitutions, CommonSubstitutions), spoilerGatedImages: true));

    private static readonly Lazy<IBbParser> SafePostParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.With(Private).Build(),
            BbParser.SecuritySubstitutions, SafeSubstitutions), spoilerGatedImages: true));

    private static readonly Lazy<IBbParser> SafeRatingParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.Build(),
            BbParser.SecuritySubstitutions, SafeSubstitutions), spoilerGatedImages: true));

    // ═════════════════════════════════════════════════════════════════════
    // AuthorEdit parsers — same tag sets as their Display counterparts
    // but with round-trip-enabled templates for [private] and [mod] so
    // Tiptap can parse and re-serialize the privacy tags without loss.
    // ═════════════════════════════════════════════════════════════════════

    private static readonly Lazy<IBbParser> PostAuthorEditParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.With(PrivateAuthorEdit).Build(),
            BbParser.SecuritySubstitutions, CommonSubstitutions)));

    private static readonly Lazy<IBbParser> CommonAuthorEditParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.With(ModAuthorEdit).Build(),
            BbParser.SecuritySubstitutions, CommonSubstitutions)));

    private static readonly Lazy<IBbParser> GeneralChatAuthorEditParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.With(ModAuthorEdit).Build(),
            BbParser.SecuritySubstitutions, CommonSubstitutions), spoilerGatedImages: true));

    /// <inheritdoc />
    public IBbParser GetForSurface(BbSurface surface) => surface switch
    {
        // Game posts: [private] allowed, [mod] not.
        BbSurface.GamePost => PostParser.Value,
        // All comments / topic bodies (forum / blog / game): [mod] allowed, [private] not.
        BbSurface.Comment => CommonParser.Value,
        // Global chat: [mod] allowed, [private] not, images behind a spoiler.
        BbSurface.GlobalChatMessage => GeneralChatMessageParser.Value,
        // Profile bios / best posts: neither [mod] nor [private]; info substitutions.
        BbSurface.Profile => InfoParser.Value,
        // Private 1-to-1 messages: neither [mod] nor [private].
        BbSurface.DirectMessage => ChatMessageParser.Value,
        _ => CommonParser.Value
    };

    /// <inheritdoc />
    public IBbParser GetSafeForSurface(BbSurface surface) => surface switch
    {
        BbSurface.GamePost => SafePostParser.Value,
        _ => SafeRatingParser.Value
    };

    /// <inheritdoc />
    public IBbParser GetForAuthorEdit(BbSurface surface) => surface switch
    {
        BbSurface.GamePost => PostAuthorEditParser.Value,
        BbSurface.Comment => CommonAuthorEditParser.Value,
        BbSurface.GlobalChatMessage => GeneralChatAuthorEditParser.Value,
        BbSurface.Profile => InfoParser.Value, // profile has neither [mod] nor [private]
        // Direct/group messages have neither [mod] nor [private] to round-trip,
        // so the author-edit path reuses the display parser (as Profile does).
        BbSurface.DirectMessage => ChatMessageParser.Value,
        _ => CommonAuthorEditParser.Value
    };
}
