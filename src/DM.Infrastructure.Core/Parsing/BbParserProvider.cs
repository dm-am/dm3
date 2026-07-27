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
    private const string ImageClassName = "image";
    private const string QuoteClassName = "quote";
    private const string QuoteHeaderClassName = "quote-author";
    private const string HeaderClassName = "info-head";
    private const string PrivateClassName = "private-message";
    private const string PrivateHeaderClassName = "private-message-header";

    private static readonly Tag Strong = new("b", "<strong>", "</strong>");
    private static readonly Tag Italic = new("i", "<em>", "</em>");
    private static readonly Tag Underlined = new("u", "<u>", "</u>");
    private static readonly Tag Strike = new("s", "<s>", "</s>");
    private static readonly Tag StrikeAlias = new("strike", "<s>", "</s>");

    // NSFW - same structure as spoiler, but with nsfw-head toggle and nsfw-spoiler content
    private static readonly Tag Nsfw = new("nsfw",
        "<a href=\"javascript:void(0)\" class=\"nsfw-head\" data-swaptext=\"?????? ?????????? ???????\">???????? ?????????? ???????</a><div class=\"nsfw-spoiler\">",
        "</div>");

    // Warning block - red highlighted block for important warnings
    private static readonly Tag Warning = new("warning", "<div class=\"warning-block\">", "</div>");

    // Mod block - green highlighted block for moderator messages (Common and Message contexts only)
    private static readonly Tag Mod = new("mod", "<div class=\"mod-block\">", "</div>");

    // AuthorEdit variant of [mod] — emits data-bb-tag so Tiptap's ModBlock
    // extension can round-trip the tag back into BBCode on save.
    private static readonly Tag ModAuthorEdit = new(
        "mod", "<div class=\"mod-block\" data-bb-tag=\"mod\">", "</div>");
    private static readonly Tag Preformatted = new("pre", $"<pre class=\"{CodeClassName}\">", "</pre>");
    private static readonly ListTag OrderedList = new("ol", "<ol>", "</ol>");
    private static readonly ListTag UnorderedList = new("ul", "<ul>", "</ul>");
    private static readonly Tag ListItem = new("li", "<li>", "</li>");

    private static readonly Tag Head = new("head", $"<h4 class=\"{HeaderClassName}\">", "</h4>");

    private static readonly Tag Spoiler = new("spoiler",
        $"<a href=\"javascript:void(0)\" class=\"{SpoilerHeadClassName}\" data-swaptext=\"Скрыть содержимое\">Показать содержимое</a><div class=\"{SpoilerClassName}\">",
        "</div>");

    private static readonly Tag Quote = new("quote",
        $"<div class=\"{QuoteClassName}\"><div class=\"{QuoteHeaderClassName}\">{{value}}</div>", "</div>", true,
        false);

    private static readonly Tag Image = new("img",
        $"<a href=\"{{value}}\" target=\"_blank\"><img src=\"{{value}}\" class=\"{ImageClassName}\" /></a>", true);

    private static readonly Tag Link = new("link", "<a href=\"{value}\">", "</a>", true);

    private static readonly Tag SafeImage = new("img",
        $"<a href=\"javascript:void(0)\" class=\"{SpoilerHeadClassName}\" data-swaptext=\"Скрыть изображение\">Показать изображение</a><div class=\"{SpoilerClassName}\"><a href=\"{{value}}\" target=\"_blank\"><img src=\"{{value}}\" class=\"{ImageClassName}\" /></a></div>",
        true);

    private static readonly Tag Tab = new("tab", "&nbsp;&nbsp;&nbsp;");

    private static readonly CodeTag Code = new("code", $"<pre class=\"{CodeClassName}\">", "</pre>");

    // Noparse - outputs content as-is without parsing inner BBCode tags
    private static readonly CodeTag Noparse = new("noparse", "", "");

    private static readonly Tag Private = new("private", $"<div class=\"{PrivateClassName}\">",
        $"</div><div class=\"{PrivateHeaderClassName}\">??????????: {{value}}</div>", true, false);

    // AuthorEdit variant of [private] — opens with data-bb-tag and
    // data-bb-addressees carrying the raw attribute value so Tiptap's
    // Private extension can round-trip the tag on save.
    private static readonly Tag PrivateAuthorEdit = new(
        "private",
        $"<div class=\"{PrivateClassName}\" data-bb-tag=\"private\" data-bb-addressees=\"{{value}}\">",
        $"</div><div class=\"{PrivateHeaderClassName}\">??????????: {{value}}</div>",
        true, false);

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
        Strong, Italic, Underlined, Strike, StrikeAlias,
        Preformatted, Spoiler, Quote, Image,
        UnorderedList, OrderedList, ListItem,
        Link, Tab, Code, Noparse, Nsfw, Warning
    });

    private static TagSetBuilder DefaultSafeTags => DefaultTags.Without(Preformatted, Image).With(SafeImage);

    // Common context: base tags + mod
    private static readonly Lazy<IBbParser> CommonParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.With(Mod).Build(),
            BbParser.SecuritySubstitutions, CommonSubstitutions)));

    // Post context: base tags + private (no mod)
    private static readonly Lazy<IBbParser> PostParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.With(Private).Build(),
            BbParser.SecuritySubstitutions, CommonSubstitutions)));

    // Info context: base tags + head (no mod, no private)
    private static readonly Lazy<IBbParser> InfoParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.With(Head).Build(),
            BbParser.SecuritySubstitutions, InfoSubstitutions)));

    // Direct/group message context: base tags only — no [mod] (private
    // chats have no moderation, so [mod] would let a user impersonate a
    // moderator) and no [private].
    private static readonly Lazy<IBbParser> ChatMessageParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.Build(),
            BbParser.SecuritySubstitutions, ChatMessageSubstitutions)));

    // General chat context: safe tags + preformatted + mod
    private static readonly Lazy<IBbParser> GeneralChatMessageParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultSafeTags.With(Preformatted, Mod).Build(),
            BbParser.SecuritySubstitutions, CommonSubstitutions)));

    private static readonly Lazy<IBbParser> SafePostParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultSafeTags.With(Private).Build(),
            BbParser.SecuritySubstitutions, SafeSubstitutions)));

    private static readonly Lazy<IBbParser> SafeRatingParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultSafeTags.Build(),
            BbParser.SecuritySubstitutions, SafeSubstitutions)));

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
        new BbParserWrapper(new BbParser(DefaultSafeTags.With(Preformatted, ModAuthorEdit).Build(),
            BbParser.SecuritySubstitutions, CommonSubstitutions)));

    /// <inheritdoc />
    public IBbParser CurrentCommon => CommonParser.Value;

    /// <inheritdoc />
    public IBbParser CurrentInfo => InfoParser.Value;

    /// <inheritdoc />
    public IBbParser GetForSurface(BbSurface surface) => surface switch
    {
        // Game posts: [private] allowed, [mod] not.
        BbSurface.GamePost => PostParser.Value,
        // All comments / topic bodies (forum / blog / game): [mod] allowed, [private] not.
        BbSurface.Comment => CommonParser.Value,
        // Global chat: [mod] allowed, [private] not, safe tag set.
        BbSurface.GlobalChatMessage => GeneralChatMessageParser.Value,
        // Profile bios / best posts: neither [mod] nor [private]; info tag set.
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