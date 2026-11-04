using System;
using System.Linq;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;
using DM.Domain.Core.Content;

namespace DM.Infrastructure.Core.Parsing.Visitors;

/// <summary>
/// The filter and transform delegates of
/// <see cref="NodeTree.ToHtml(System.Func{Node, bool}, System.Func{Node, string, string})"/>
/// for the hiding tags (<see cref="BBCodeParser.Tags.Tag.Hides"/>) and for the
/// pictures and links of files the viewer does not read
/// (<see cref="RenderContext.WithheldUploadIds"/>, <see cref="RenderContext.WithholdsEveryUpload"/>). [mod] is public on read and gated
/// on write (ModBlockSanitizer). A node the filter refuses is skipped with its
/// subtree, leaving no trace.
/// </summary>
public static class PermissionFilter
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

    private static bool IsNodeVisible(Node node, RenderContext ctx)
    {
        if (node is TagNode { Tag: ContentTag } contentNode) return !NamesWithheldUpload(contentNode, ctx);

        if (node is not TagNode { Tag.Hides: true } tagNode) return true;

        if (ctx.Audience is RenderAudience.PlainText or RenderAudience.EmbedSafe)
            return false;

        return IsPrivateVisible(tagNode, ctx);
    }

    /// <summary>Whether the address an [img] or a [link] holds names a file the viewer does not read.</summary>
    private static bool NamesWithheldUpload(TagNode node, RenderContext ctx) =>
        (ctx.WithholdsEveryUpload || ctx.WithheldUploadIds.Count > 0) &&
        node.Children.OfType<TextNode>().Any(text => ctx.WithholdsEveryUpload
            ? UploadIdentifiers.Mentioned(text.Text)
            : ctx.WithheldUploadIds.Any(id => PrivateBlockReading.Mentions(text.Text, id)));

    private static bool IsPrivateVisible(TagNode tagNode, RenderContext ctx)
    {
        // Before the author rule on purpose: off the game post nobody reads it.
        if (ctx.Surface != BbSurface.GamePost) return false;

        // A broken block addresses nobody: who it was meant for cannot be read from it.
        // Owners resolved at save time, keyed by the attribute as the author wrote it.
        var attribute = tagNode.IsBroken ? null : tagNode.AttributeValue ?? string.Empty;
        var owners = attribute != null &&
                     ctx.PrivateAddresseeOwnerUserIdsByAttribute.TryGetValue(attribute, out var allowed)
            ? allowed
            : null;

        // Never off the viewer: null matches nothing (AnonymousIdentity). The query
        // layer has already checked that the viewer reads the room.
        return PrivateBlockReaders.Reads(
            ctx.ViewerUserId,
            ctx.PostAuthorUserId,
            ctx.GameLeadUserIds,
            owners,
            PrivateBlockMarkup.NamesAddressees(attribute),
            ctx.GameShowPrivateMessages,
            ctx.RoomViewPrivateText);
    }

    /// <summary>
    /// The attribute value the tag templates substitute, per audience: the
    /// recipients on a page, the attribute as written elsewhere.
    /// </summary>
    private static string Transform(Node node, string rendered, RenderContext ctx) =>
        ctx.Audience == RenderAudience.Display ? Recipients(node, rendered, ctx) : rendered;

    /// <summary>
    /// The recipients of a [private] block for its tooltip: the frozen addressees,
    /// which survive a rename, else the text of the tag.
    /// </summary>
    private static string Recipients(Node node, string rendered, RenderContext ctx)
    {
        if (node is not TagNode { Tag.Hides: true } tagNode || tagNode.IsBroken) return rendered;

        return ctx.PrivateAddresseeNamesByAttribute.TryGetValue(rendered ?? string.Empty, out var names) &&
               names.Count > 0
            ? string.Join(", ", names)
            : rendered ?? string.Empty;
    }
}
