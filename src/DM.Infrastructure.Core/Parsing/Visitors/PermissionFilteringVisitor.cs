using System;
using System.Collections.Generic;
using BBCodeParser.Nodes;
using DM.Domain.Core.Content;

namespace DM.Infrastructure.Core.Parsing.Visitors;

/// <summary>
/// Produces filter + transform delegates compatible with
/// <see cref="NodeTree.ToHtml(System.Func{Node, bool}, System.Func{Node, string, string})"/>
/// that enforce DM3's privacy tag visibility rules.
///
/// The privacy-sensitive tags are the ones the tag catalogue declares hiding
/// (<see cref="BBCodeParser.Tags.Tag.Hides"/>), which is [private]: read on every
/// surface, shown on game posts alone.
/// [mod] is not filtered here: it is public on read (everyone sees an authored
/// mod block) and restricted on write (unauthorized [mod] is unwrapped at save
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

    // ───────────────────────────────────────────────────────────────────
    // FILTER: which nodes are visible to the current viewer
    // ───────────────────────────────────────────────────────────────────

    private static bool IsNodeVisible(Node node, RenderContext ctx)
    {
        // Only a hiding tag node is ever filtered; plain text and every other tag
        // always pass through.
        if (node is not TagNode { Tag.Hides: true } tagNode) return true;

        // PlainText / EmbedSafe: strip the hiding tag unconditionally.
        if (ctx.Audience is RenderAudience.PlainText or RenderAudience.EmbedSafe)
            return false;

        // AuthorEdit: always visible. The audience only reaches this visitor
        // after the render pipeline matched the viewer against the author id in
        // the envelope; a mismatch is downgraded to Display before the context
        // is built.
        if (ctx.Audience == RenderAudience.AuthorEdit)
            return true;

        return IsPrivateVisible(tagNode, ctx);
    }

    private static bool IsPrivateVisible(TagNode tagNode, RenderContext ctx)
    {
        // [private] is only meaningful in game posts. Anywhere else nobody reads
        // it, its author included, and the check stands before the author rule
        // on purpose. The save path refuses such a block with a reason, so what
        // arrives here never went through it: stored before the refusal existed,
        // or carried over from DM2, where the author is not around to be told.
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

        // A block the grammar did not accept addresses nobody, whatever its
        // spelling seems to name: which reader it was meant for is exactly what
        // cannot be read from it. It stays with the author and the leads.
        if (tagNode.IsBroken) return false;

        // Addressee-forever: the character owner of an addressed character
        // sees the block. Resolved per-block at post save time, keyed by the tag
        // attribute exactly as the author wrote it — which is what the parser
        // reports, nothing having rewritten or encoded it on the way in.
        var attribute = tagNode.AttributeValue ?? string.Empty;
        if (viewerId.HasValue &&
            ctx.PrivateAddresseeOwnerUserIdsByAttribute.TryGetValue(attribute, out var allowed) &&
            allowed.Contains(viewerId.Value))
            return true;

        // The two overrides share text addressed to characters, and only that.
        // A bare [private] names no character: it is text for the leads, and a
        // setting that shares the addressed lines of a post or a room is not a
        // decision about it.
        if (!PrivateBlockMarkup.NamesAddressees(attribute)) return false;

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
    // opening markup and the closing markup both, encoded there. It cannot
    // augment the HTML around a tag, so AuthorEdit round-trip attributes are
    // emitted by dedicated tag templates in BbParserProvider instead.
    // ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// The attribute value the tag templates substitute, per audience.
    /// </summary>
    /// <remarks>
    /// AuthorEdit is left alone on purpose. Its [private] template writes the
    /// value into <c>data-bb-addressees</c>, which the editor turns back into
    /// the tag on save: rewriting it there would edit the author's own text
    /// behind their back.
    /// </remarks>
    private static string Transform(Node node, string rendered, RenderContext ctx) =>
        ctx.Audience switch
        {
            RenderAudience.Display => Recipients(node, rendered, ctx),
            _ => rendered
        };

    /// <summary>
    /// The names a [private] block carries as its recipients, for the tooltip
    /// over it: the frozen addressees, not the text of the tag.
    /// </summary>
    /// <remarks>
    /// The two disagree the moment a character is renamed. The tag keeps the
    /// name the author typed - it is their text and it stays as written - but
    /// the name is only a way of pointing at a character, and it can be given
    /// up and taken by another. The block still goes to the character the
    /// snapshot froze it to, so the recipients have to name that character;
    /// composed from the tag they name whoever answers to the name today, who
    /// may never have seen the block.
    ///
    /// No entry, no names: an old snapshot names nobody, and a block that
    /// resolved to nobody has nobody to name. Both fall back to the author's
    /// text, which is what the recipients have always named. The tag names the
    /// leads in front of what is returned here and encodes the list where it
    /// writes it (PrivateTag).
    /// </remarks>
    private static string Recipients(Node node, string rendered, RenderContext ctx)
    {
        if (node is not TagNode { Tag.Hides: true } tagNode || tagNode.IsBroken) return rendered;

        return ctx.PrivateAddresseeNamesByAttribute.TryGetValue(rendered ?? string.Empty, out var names) &&
               names.Count > 0
            ? string.Join(", ", names)
            : rendered ?? string.Empty;
    }

    private static bool ContainsGuid(IReadOnlyCollection<Guid> set, Guid value)
    {
        foreach (var item in set)
            if (item == value)
                return true;
        return false;
    }
}
