using System;
using System.Collections.Generic;
using BBCodeParser.Nodes;

namespace DM.Infrastructure.Core.Parsing.Visitors;

/// <summary>
/// Produces filter + transform delegates compatible with
/// <see cref="NodeTree.ToHtml(System.Func{Node, bool}, System.Func{Node, string, string})"/>
/// that enforce DM3's privacy tag visibility rules.
///
/// The only privacy-sensitive tag is [private] (game posts). [mod] is not
/// filtered here: it is public on read (everyone sees an authored mod
/// block) and restricted on write (unauthorized [mod] is unwrapped at save
/// time by ModBlockSanitizer, so it never reaches stored content).
///
/// This is not a mutating visitor — the delegates returned here are pure
/// functions of the node and the render context, invoked by the tree
/// walker during emission. Stripping a [private] block means the filter
/// returns false for the corresponding <see cref="TagNode"/>; the walker
/// then skips the entire subtree (zero-information erase, no placeholder,
/// no marker comment, no gap).
/// </summary>
public static class PermissionFilteringVisitor
{
    /// <summary>Tag name for the private addressee block.</summary>
    public const string PrivateTagName = "private";

    /// <summary>Tag name for the quotation block.</summary>
    public const string QuoteTagName = "quote";

    /// <summary>The filter and transform delegates the tree walker needs.</summary>
    public readonly record struct FilterPlan(
        Func<Node, bool> Filter,
        Func<Node, string, string> Transform);

    /// <summary>
    /// Build a filter plan for the given render context.
    /// </summary>
    public static FilterPlan Prepare(RenderContext ctx) =>
        new(Filter: node => IsNodeVisible(node, ctx),
            Transform: (node, rendered) => Transform(node, rendered, ctx));

    /// <summary>
    /// Check whether a tag name is privacy-sensitive (subject to filtering).
    /// Only [private] qualifies — [mod] is public on read.
    /// </summary>
    public static bool IsPrivacySensitiveTag(string? tagName) =>
        tagName is PrivateTagName;

    // ───────────────────────────────────────────────────────────────────
    // FILTER: which nodes are visible to the current viewer
    // ───────────────────────────────────────────────────────────────────

    private static bool IsNodeVisible(Node node, RenderContext ctx)
    {
        // Only tag nodes are ever filtered; plain text always passes through.
        if (node is not TagNode tagNode) return true;

        var tagName = tagNode.Tag?.Name;
        if (tagName is null) return true;

        // QuoteSource: the two tags a quotation never carries, whoever is
        // asking. [private] because quoting is republication and the addressee
        // snapshot does not travel with the text - the block would be shown by
        // the new post's rules, to a different set of people - so it is dropped
        // for the post's own author and for a game lead as well. [quote]
        // because a quotation of a quotation grows the chain by one level on
        // every reply; the nesting guard in the parser stands against a page
        // that cannot be rendered, not against this.
        if (ctx.Audience == RenderAudience.QuoteSource)
            return tagName is not (PrivateTagName or QuoteTagName);

        // Non-privacy tags are always visible.
        if (!IsPrivacySensitiveTag(tagName)) return true;

        // PlainText / EmbedSafe: strip the privacy-sensitive tag. Only [private]
        // reaches this point — [mod] is public on read and returns visible above.
        if (ctx.Audience is RenderAudience.PlainText or RenderAudience.EmbedSafe)
            return false;

        // AuthorEdit: both tags are always visible. The audience only
        // reaches this visitor after the render pipeline matched the viewer
        // against the author id in the envelope; a mismatch is downgraded
        // to Display before the context is built.
        if (ctx.Audience == RenderAudience.AuthorEdit)
            return true;

        // Display audience: per-tag rules. [private] is the only
        // privacy-sensitive tag; every other tag already returned true above.
        return tagName switch
        {
            PrivateTagName => IsPrivateVisible(tagNode, ctx),
            _ => true
        };
    }

    private static bool IsPrivateVisible(TagNode tagNode, RenderContext ctx)
    {
        // [private] is only meaningful in game posts.
        if (ctx.Surface != BbSurface.GamePost) return false;

        // Through the context, never off the viewer: an anonymous reader carries
        // the empty id, and so does every context field a projection forgot to
        // fill, so the two match each other on rule after rule below. The
        // context answers null for a reader there is no id for, and null matches
        // nothing (see AnonymousIdentity).
        var viewerId = ctx.ViewerUserId;

        // Author-forever: the post author always sees their own [private] blocks.
        if (viewerId.HasValue && ctx.PostAuthorUserId.HasValue &&
            ctx.PostAuthorUserId.Value == viewerId.Value)
            return true;

        // Game leads (master + assistants) always see all [private] blocks.
        if (viewerId.HasValue && ContainsGuid(ctx.GameLeadUserIds, viewerId.Value))
            return true;

        // Addressee-forever: the character owner of an addressed character
        // sees the block. Resolved per-block at post save time, keyed by the raw
        // tag attribute string - which is not what the parser reports, because
        // the render path encodes the value before the parser ever sees it. So
        // the encoding is undone here, at the one place that compares the two
        // (see BbAttributeEncoding). Without that step every name carrying a
        // character HTML gives meaning to - an apostrophe is enough, D'Artagnan
        // is an ordinary name - missed its own entry, and the block was hidden
        // from the player it was addressed to. Safe and silent, which is why
        // nobody reported it.
        var attribute = BbAttributeEncoding.Decode(tagNode.AttributeValue);
        if (viewerId.HasValue &&
            ctx.PrivateAddresseeOwnerUserIdsByAttribute.TryGetValue(attribute, out var allowed) &&
            allowed.Contains(viewerId.Value))
            return true;

        // Per-post override: author marked the post as "share private with all".
        // Pre-condition: the caller has already verified the viewer can read
        // the room (room access gate is at the query layer, not here).
        if (ctx.PostSharePrivateWithAll) return true;

        // Per-room override: the room is configured to show private text to
        // all room readers.
        if (ctx.RoomViewPrivateText) return true;

        // Viewer does not qualify through any rule.
        return false;
    }

    // ───────────────────────────────────────────────────────────────────
    // TRANSFORM: the callback rewrites a tag's attribute value, and the tag
    // templates substitute what it returns wherever they write {value} - the
    // opening markup and the closing markup both. It cannot augment the HTML
    // around a tag, so AuthorEdit round-trip attributes are emitted by
    // dedicated tag templates in BbParserProvider instead.
    //
    // It used not to run at depth at all: the recursive TagNode.ToHtml passed
    // only the filter down to its children and dropped this callback, so it
    // ran on the children of the root and on nobody deeper - silently, which
    // is the worst way for a transform to not run. The single iterative walk
    // carries both delegates to every node.
    // ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// The attribute value the tag templates substitute, per audience.
    /// </summary>
    /// <remarks>
    /// What the parser reports is the encoded value, not the written one -
    /// <see cref="BbParserWrapper"/> HTML-encodes every attribute the parser
    /// substitutes into markup, on the exact string it hands over. On the HTML
    /// path that is invisible: the browser decodes the entities back for the
    /// reader. On the source path there is no browser, so an author who quoted
    /// <c>[spoiler="A &amp; B"]</c> would get <c>A &amp;amp; B</c> into the composer
    /// and would save it that way. Decode is the exact inverse of Encode (see
    /// <see cref="BbAttributeEncoding"/>), so the value comes back as written -
    /// and goes through the same encoding again when the reply is saved.
    ///
    /// AuthorEdit is left alone on purpose. Its [private] template writes the
    /// value into <c>data-bb-addressees</c>, which the editor turns back into
    /// the tag on save: rewriting it there would edit the author's own text
    /// behind their back.
    /// </remarks>
    private static string Transform(Node node, string rendered, RenderContext ctx) =>
        ctx.Audience switch
        {
            RenderAudience.QuoteSource => BbAttributeEncoding.Decode(rendered),
            RenderAudience.Display => RecipientsLine(node, rendered, ctx),
            _ => rendered
        };

    /// <summary>
    /// The names the recipients line under a [private] block prints: the frozen
    /// addressees, not the text of the tag.
    /// </summary>
    /// <remarks>
    /// The two disagree the moment a character is renamed. The tag keeps the
    /// name the author typed - it is their text and it stays as written - but
    /// the name is only a way of pointing at a character, and it can be given
    /// up and taken by another. The block still goes to the character the
    /// snapshot froze it to, so the line has to name that character; composed
    /// from the tag it names whoever answers to the name today, who may never
    /// have seen the block.
    ///
    /// The value is encoded on the way out because the [private] tag is
    /// declared insecure - the parser substitutes its attribute as it stands -
    /// and a character name is user text. What arrives here is already encoded,
    /// by <see cref="BbParserWrapper"/>, which is why the lookup decodes first.
    ///
    /// No entry, no names: an old snapshot names nobody, and a block that
    /// resolved to nobody has nobody to name. Both fall back to the author's
    /// text, which is what this line has always printed.
    /// </remarks>
    private static string RecipientsLine(Node node, string rendered, RenderContext ctx)
    {
        if (node is not TagNode tagNode || tagNode.Tag?.Name != PrivateTagName) return rendered;

        var attribute = BbAttributeEncoding.Decode(rendered);
        return ctx.PrivateAddresseeNamesByAttribute.TryGetValue(attribute, out var names) &&
               names.Count > 0
            ? BbAttributeEncoding.Encode(string.Join(", ", names))
            : rendered;
    }

    private static bool ContainsGuid(IReadOnlyCollection<Guid> set, Guid value)
    {
        foreach (var item in set)
            if (item == value)
                return true;
        return false;
    }
}
