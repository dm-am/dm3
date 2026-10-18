using System;
using System.Collections.Generic;
using BBCodeParser;
using BBCodeParser.Tags;
using DM.Domain.Core.Content;
using DM.Infrastructure.Core.Parsing.Tags;

namespace DM.Infrastructure.Core.Parsing;

/// <inheritdoc />
/// <remarks>
/// The tag catalogue: which tags each surface reads and what each renders to.
/// The grammar every tag is read by lives in the parser (see BBCodeParser.Reader),
/// and docs/architecture/BBCODE_RENDERING.md, section "Грамматика разметки", is
/// the rule both are held to.
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

    // Every block of the reading - [spoiler], [nsfw], [quote], [mod], the lists
    // and their items - leaves out the one line break typed straight after its
    // closing tag (Tag.DropsLineBreakAfter). Its element ends the line already,
    // and the break drew an empty line under the block that the text over it
    // never had and the editor's preview does not draw. The author-edit variants
    // further down do not carry it: the editor gets every break back. [private]
    // is no block: it stands in the line as text does (see the tag below).

    // NSFW - same structure as spoiler, but with nsfw-head toggle and nsfw-spoiler content.
    // href="#" for the reason SpoilerTag gives.
    private static readonly Tag Nsfw = new("nsfw",
        "<a href=\"#\" class=\"nsfw-head\" data-swaptext=\"Скрыть шокирующий контент\">Показать шокирующий контент</a><div class=\"nsfw-spoiler\">",
        "</div>")
    {
        DropsLineBreakAfter = true
    };

    // Mod block - green highlighted block for moderator messages (Common and Message contexts only)
    private static readonly Tag Mod = new("mod", "<div class=\"mod-block\">", "</div>") { DropsLineBreakAfter = true };

    // AuthorEdit variant of [mod] — emits data-bb-tag so Tiptap's ModBlock
    // extension can round-trip the tag back into BBCode on save.
    private static readonly Tag ModAuthorEdit = new(
        "mod", "<div class=\"mod-block\" data-bb-tag=\"mod\">", "</div>");
    private static readonly ListTag OrderedList = new("ol", "<ol>", "</ol>") { DropsLineBreakAfter = true };
    private static readonly ListTag UnorderedList = new("ul", "<ul>", "</ul>") { DropsLineBreakAfter = true };
    private static readonly Tag ListItem = new ListItemTag { DropsLineBreakAfter = true };

    private static readonly Tag Spoiler = new SpoilerTag(authorEdit: false);

    private static readonly Tag Quote = new("quote", $"<div class=\"{QuoteClassName}\">", "</div>")
    {
        DropsLineBreakAfter = true
    };

    // AuthorEdit variants of the base tags, for the same reason the privacy
    // tags have theirs: the editor rebuilds each tag from its data-bb-tag.
    //
    // Spoiler and NSFW are the block alone. The head that opens each of them is
    // for a reader, as the recipients of [private] are: it carries no data-bb-*
    // marker, so the reverse conversion kept its caption as text and every edit
    // saved one more line of it above the block.
    private static readonly Tag SpoilerAuthorEdit = new SpoilerTag(authorEdit: true);

    private static readonly Tag NsfwAuthorEdit = new(
        "nsfw", "<div class=\"nsfw-spoiler\" data-bb-tag=\"nsfw\">", "</div>");

    private static readonly Tag QuoteAuthorEdit = new(
        "quote", $"<div class=\"{QuoteClassName}\" data-bb-tag=\"quote\">", "</div>");

    // [tab] and [noparse] have no element on read, so here they get one to carry
    // the marker. Without it [tab] came back as three spaces, and the content of
    // [noparse] as bare text whose tags became live markup on the next save.
    private static readonly Tag TabAuthorEdit = new("tab", "<span data-bb-tag=\"tab\">&nbsp;&nbsp;&nbsp;</span>");

    private static readonly CodeTag NoparseAuthorEdit = new(
        "noparse", "<span data-bb-tag=\"noparse\">", "</span>");

    private static readonly Tag Tab = new("tab", "&nbsp;&nbsp;&nbsp;");

    // [code] as on DM2: a monospace font and nothing else. It stands in the line
    // like [b], and the markup inside it works as it does anywhere - tags,
    // dashes, a hiding block, which hides. Its element is an inline one for that
    // reason; the block <pre> it used to be drew every [code] on a line of its
    // own. The editor reads the element back as [code], so the author-edit
    // rendering needs no variant of it.
    private static readonly Tag Code = new("code", $"<code class=\"{CodeClassName}\">", "</code>");

    // [noparse] shows its content as typed: the one block the parser reads as a
    // piece, up to its own closing tag, with nothing inside it markup (see
    // BBCodeParser.Tags.CodeTag). It is the way to show markup as an example.
    private static readonly CodeTag Noparse = new("noparse", "", "");

    // [img], [link] and [mention] are nodes of the parse like every other tag:
    // what stands between their two halves is read by the grammar first, so a
    // hiding block written inside one is a hiding block. Everything that makes
    // an address safe to put on a page is in BbAddress.
    private static readonly Tag Image = new ImageTag(ImageGate.None);
    private static readonly Tag ChatImage = new ImageTag(ImageGate.ReaderChoice);
    private static readonly Tag SafeImage = new ImageTag(ImageGate.Always);
    private static readonly Tag Link = new LinkTag();
    private static readonly Tag Mention = new MentionTag();

    // Sealed: only [/private] ends it. A closing tag of some other name that
    // stands inside the block used to end it early, and the rest of the block -
    // written by its author as private text - was printed to the whole room.
    // See Tag.SealedByOwnTag for the worked example; [mod] is deliberately not
    // sealed, being public on read, so ending it early costs a reader nothing
    // but formatting.
    //
    // Hiding: every spelling of its opening is a block, and the visibility filter
    // decides who reads one the grammar did not accept (see Tag.Hides and
    // PermissionFilteringVisitor).
    //
    // Read on every surface, and shown on the game post alone. Anywhere else the
    // save path refuses it, and text that never went through the save path -
    // stored before the refusal, or carried over from DM2 - still carries it: had
    // the tag been missing from the set there, the block would have been text,
    // printed with its secret to every reader. Read, it is a node, and the filter
    // erases it for every reader of a surface that does not allow it, its author
    // included.
    //
    // As on DM2 the block is text in the line: green words in a span, so it ends
    // no line and draws no box. Who reads it is not text: the list rides on the
    // span in data-recipients, and the page shows it in a tooltip over the green
    // words (bbcodeInteractive). The list names the leads first, in the words DM2
    // posts use for them and in the same plain text as any name, and then the
    // names of the block; a block that names nobody names the leads alone (see
    // PrivateTag).
    private static readonly Tag Private = new PrivateTag(
        $"<span class=\"{PrivateClassName}\" data-recipients=\"{{value}}\">", "</span>");

    // AuthorEdit variant of [private] — opens with data-bb-tag and
    // data-bb-addressees carrying the attribute value so Tiptap's Private
    // extension can round-trip the tag on save.
    //
    // It carries no recipients, unlike the reading variant above. They are for
    // a reader, and the same names are already in data-bb-addressees, which is
    // what the tag is rebuilt from.
    //
    // A block the grammar did not accept, or that nobody closed, is given to its
    // author as it was typed: the save path refuses it and names the fix, and a
    // block the editor rebuilt in the right shape would hide the spelling the
    // refusal talks about.
    private static readonly Tag PrivateAuthorEdit = new(
        PrivateBlockMarkup.TagName,
        $"<span class=\"{PrivateClassName}\" data-bb-tag=\"private\" data-bb-addressees=\"{{value}}\">",
        "</span>",
        withAttribute: true,
        sealedByOwnTag: true)
    {
        Hides = true,
        MalformedAsWritten = true
    };

    // A line break written as CR LF is one break. The browser turns the CR it
    // parses into LF, and [code] keeps its line breaks (white-space: pre-wrap):
    // "\r<br />" drew an empty line under every line of a [code] stored with
    // CR LF, the way a DM2 form sent it, while the editor's preview drew one
    // break. A CR alone is left as it is, as the preview leaves it. The order
    // matters: CR LF first.
    private static readonly Dictionary<string, string> CommonSubstitutions = new()
    {
        {"---", "&mdash;"},
        {"--", "&ndash;"},
        {"\r\n", "<br />"},
        {"\n", "<br />"}
    };

    // The same typography in the plain text, where an entity is not markup but
    // six more characters: the dashes as the characters themselves, and the
    // line break left as it is.
    private static readonly Dictionary<string, string> CommonTextSubstitutions = new()
    {
        {"---", "\u2014"},
        {"--", "\u2013"}
    };

    // The author's editor is filled with the text as it was typed. The typography
    // ("--" as a dash) has no way back into BBCode: htmlToBbcode leaves the
    // entity in the text as it is, so the author's next save wrote "&ndash;"
    // where "--" had been typed. The line break stays, being the one substitution
    // the editor reads back.
    //
    // A line break written as CR LF, or as a CR alone, is one break here as
    // well. The editor cannot hold a CR: the browser turns every CR it parses
    // into LF, so "\r\n" rendered as "\r<br />" reached the editor as two
    // breaks, and the first edit of a text stored with CR LF (the way a DM2 form
    // sent it) doubled every line break of it. The order matters: CR LF first.
    private static readonly Dictionary<string, string> AuthorEditSubstitutions = new()
    {
        {"\r\n", "<br />"},
        {"\n", "<br />"},
        {"\r", "<br />"}
    };

    // Base tags available in all contexts, with the image tag of the rendering.
    // [private] is among them: it is read everywhere and shown on the game post
    // alone (see the tag above).
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

    // Common context: base tags + mod
    private static readonly Lazy<IBbParser> CommonParser = new(() => Display(DefaultTags(Image).With(Mod)));

    // Post context: base tags, no mod. The one surface whose [private] anybody reads.
    private static readonly Lazy<IBbParser> PostParser = new(() => Display(DefaultTags(Image)));

    // Info context: base tags, no mod. The set is the game post's; what differs
    // is the filter's answer about [private], which here is nobody.
    private static readonly Lazy<IBbParser> InfoParser = new(() => Display(DefaultTags(Image)));

    // Direct/group message context: base tags only — no [mod] (private
    // chats have no moderation, so [mod] would let a user impersonate a
    // moderator).
    //
    // The line break is drawn here as on every other surface. The message is
    // shown in a block that collapses white space, so a break left as typed
    // read as a space and a message written on three lines arrived on one.
    private static readonly Lazy<IBbParser> ChatMessageParser = new(() => Display(DefaultTags(Image)));

    // General chat context: base tags + mod, with images behind a spoiler the
    // reader's layout opens or keeps closed.
    private static readonly Lazy<IBbParser> GeneralChatMessageParser = new(() =>
        Display(DefaultTags(ChatImage).With(Mod)));

    // "Safe" is one thing: images behind a spoiler, always, and [private] cut
    // out for everybody (PermissionFilteringVisitor), whatever the surface.
    private static readonly Lazy<IBbParser> SafeParser = new(() => Display(DefaultTags(SafeImage)));

    // ═════════════════════════════════════════════════════════════════════
    // The hint: the description of a game tag, shown in its tooltip
    // (BbSurface.Hint). One tag, [tip], and the two every surface has for its
    // own safety: [noparse], which shows an example as typed (and which the
    // refusal of a hidden block names as the way to write one), and [private],
    // read so that the filter erases it for every reader rather than print it.
    //
    // A line break is drawn as a break. Nothing else is substituted: the dashes
    // of a post are DM2 typography, and a description has always been shown as
    // typed, which is also what the moderator's preview draws (bbcode.ts,
    // hintToHtml). The plain text is the text as typed.
    // ═════════════════════════════════════════════════════════════════════

    private static readonly Tag Tip = new TipTag();

    private static readonly Dictionary<string, string> HintSubstitutions = new()
    {
        {"\r\n", "<br />"},
        {"\n", "<br />"}
    };

    private static readonly Lazy<IBbParser> HintParser = new(() =>
        new BbParserWrapper(new BbParser(new TagSetBuilder([Tip, Noparse, Private]).Build(),
            BbParser.SecuritySubstitutions, HintSubstitutions)));

    // ═════════════════════════════════════════════════════════════════════
    // AuthorEdit parsers — same tag sets as their Display counterparts, but
    // every tag whose reading markup the editor cannot turn back into BBCode
    // is swapped for its AuthorEdit variant above, and none of the typography
    // is applied. BbAuthorEditRoundTripShould holds each tag and substitution
    // of each surface to the client's reverse conversion.
    //
    // [private] is the one tag of a display set missing here outside the game
    // post: the author gets such a block back as the text they typed, sees it,
    // and is told by the save path why it cannot stay. Rebuilt as a block, it
    // would be saved back as a block and refused without the author having
    // seen what the refusal is about.
    // ═════════════════════════════════════════════════════════════════════

    // The lists of the author's markup are the tags as written, in elements the
    // browser keeps where they stand. The reading lists lift the text between
    // their items out of the list and drop the line breaks between items (see
    // TagNode.EmitList), which is right for a reader and rewrote the author's
    // text on every edit: [ul]a[li]b[/li][/ul] came back as a[ul][li]b[/li][/ul],
    // and a list holding only text came back without its tags. <li> is not used
    // either: a browser closes an open item where another opens with no list
    // between them, so [li]a[li]b[/li]c[/li] came back as three pieces.
    private static readonly Tag UnorderedListAuthorEdit = new("ul", "<div data-bb-tag=\"ul\">", "</div>");
    private static readonly Tag OrderedListAuthorEdit = new("ol", "<div data-bb-tag=\"ol\">", "</div>");
    private static readonly Tag ListItemAuthorEdit = new("li", "<div data-bb-tag=\"li\">", "</div>");

    private static TagSetBuilder AuthorEditTags => new([
        Strong, Italic, Underlined, Strike,
        SpoilerAuthorEdit, QuoteAuthorEdit,
        UnorderedListAuthorEdit, OrderedListAuthorEdit, ListItemAuthorEdit,
        TabAuthorEdit, Code, NoparseAuthorEdit, NsfwAuthorEdit,
        Image, Link, Mention
    ]);

    private static readonly Lazy<IBbParser> PostAuthorEditParser = new(() =>
        new BbParserWrapper(new BbParser(AuthorEditTags.With(PrivateAuthorEdit).Build(),
            BbParser.SecuritySubstitutions, AuthorEditSubstitutions)));

    // Global chat shares this one: the spoiler its images are put behind guards a
    // reader, and in front of the author that gate came back as a [spoiler] around
    // the image, with the gate's caption as text above it.
    private static readonly Lazy<IBbParser> CommonAuthorEditParser = new(() =>
        new BbParserWrapper(new BbParser(AuthorEditTags.With(ModAuthorEdit).Build(),
            BbParser.SecuritySubstitutions, AuthorEditSubstitutions)));

    private static readonly Lazy<IBbParser> InfoAuthorEditParser = new(() =>
        new BbParserWrapper(new BbParser(AuthorEditTags.Build(),
            BbParser.SecuritySubstitutions, AuthorEditSubstitutions)));

    private static readonly Lazy<IBbParser> ChatMessageAuthorEditParser = new(() =>
        new BbParserWrapper(new BbParser(AuthorEditTags.Build(),
            BbParser.SecuritySubstitutions, AuthorEditSubstitutions)));

    /// <inheritdoc />
    /// <remarks>
    /// "[private] not" below means the block is read and erased for every reader,
    /// not that it is text: see the tag.
    /// </remarks>
    public IBbParser GetForSurface(BbSurface surface) => surface switch
    {
        // Game posts: [private] allowed, [mod] not.
        BbSurface.GamePost => PostParser.Value,
        // All comments / topic bodies (forum / blog / game): [mod] allowed, [private] not.
        BbSurface.Comment => CommonParser.Value,
        // Global chat: [mod] allowed, [private] not, images behind a spoiler.
        BbSurface.GlobalChatMessage => GeneralChatMessageParser.Value,
        // Profile bios / best posts: neither [mod] nor [private].
        BbSurface.Profile => InfoParser.Value,
        // Private 1-to-1 messages: neither [mod] nor [private].
        BbSurface.DirectMessage => ChatMessageParser.Value,
        // Tag descriptions: [tip] alone, [private] not.
        BbSurface.Hint => HintParser.Value,
        _ => CommonParser.Value
    };

    /// <inheritdoc />
    /// <remarks>
    /// The hint has no safe variant: it shows no image on sight, and the safe
    /// parser would read the tags of a post in it.
    /// </remarks>
    public IBbParser GetSafeForSurface(BbSurface surface) =>
        surface == BbSurface.Hint ? HintParser.Value : SafeParser.Value;

    /// <inheritdoc />
    /// <remarks>
    /// The hint has no author's variant: a moderator edits the description as
    /// the raw string, and nothing reads the markup back.
    /// </remarks>
    public IBbParser GetForAuthorEdit(BbSurface surface) => surface switch
    {
        BbSurface.GamePost => PostAuthorEditParser.Value,
        BbSurface.Comment => CommonAuthorEditParser.Value,
        BbSurface.GlobalChatMessage => CommonAuthorEditParser.Value,
        BbSurface.Profile => InfoAuthorEditParser.Value,
        BbSurface.DirectMessage => ChatMessageAuthorEditParser.Value,
        BbSurface.Hint => HintParser.Value,
        _ => CommonAuthorEditParser.Value
    };
}
