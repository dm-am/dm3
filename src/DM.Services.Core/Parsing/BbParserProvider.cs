using System;
using System.Collections.Generic;
using BBCodeParser;
using BBCodeParser.Tags;

namespace DM.Services.Core.Parsing;

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
        "<a href=\"javascript:void(0)\" class=\"nsfw-head\" data-swaptext=\"Скрыть шокирующий контент\">Показать шокирующий контент</a><div class=\"nsfw-spoiler\">",
        "</div>");

    // Warning block - red highlighted block for important warnings
    private static readonly Tag Warning = new("warning", "<div class=\"warning-block\">", "</div>");

    // Mod block - green highlighted block for moderator messages (Common and Message contexts only)
    private static readonly Tag Mod = new("mod", "<div class=\"mod-block\">", "</div>");
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
        $"</div><div class=\"{PrivateHeaderClassName}\">Получатели: {{value}}</div>", true, false);

    private static readonly Dictionary<string, string> CommonSubstitutions = new()
    {
        {"---", "&mdash;"},
        {"--", "&ndash;"},
        {"\n", "<br />"}
    };

    private static readonly Dictionary<string, string> ConversationMessageSubstitutions =
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

    // Conversation/Message context: base tags + mod
    private static readonly Lazy<IBbParser> ConversationMessageParser = new(() =>
        new BbParserWrapper(new BbParser(DefaultTags.With(Mod).Build(),
            BbParser.SecuritySubstitutions, ConversationMessageSubstitutions)));

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

    /// <inheritdoc />
    public IBbParser CurrentCommon => CommonParser.Value;

    /// <inheritdoc />
    public IBbParser CurrentInfo => InfoParser.Value;

    /// <inheritdoc />
    public IBbParser CurrentConversationMessage => ConversationMessageParser.Value;

    /// <inheritdoc />
    public IBbParser CurrentPost => PostParser.Value;

    /// <inheritdoc />
    public IBbParser CurrentSafePost => SafePostParser.Value;

    /// <inheritdoc />
    public IBbParser CurrentSafeRating => SafeRatingParser.Value;

    /// <inheritdoc />
    public IBbParser CurrentGeneralChat => GeneralChatMessageParser.Value;
}