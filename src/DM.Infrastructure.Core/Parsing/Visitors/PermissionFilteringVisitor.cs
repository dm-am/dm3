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

    /// <summary>
    /// Prepared plan: the filter and transform delegates the tree walker
    /// needs, plus metadata (whether the content contains any privacy-
    /// sensitive tags) that callers use to pick a cache bucket.
    /// </summary>
    public readonly record struct FilterPlan(
        Func<Node, bool> Filter,
        Func<Node, string, string> Transform,
        bool HasPrivacyTags);

    /// <summary>
    /// Build a filter plan for the given parsed tree + render context.
    /// Detects privacy-sensitive tags by scanning the raw source string
    /// (the BBCodeParser node tree does not expose child enumeration
    /// from outside its assembly). False positives produced by plain-text
    /// occurrences of the literal tokens are harmless — they only force
    /// a per-user bucket when a coarse bucket would have sufficed, never
    /// the other way around.
    /// </summary>
    public static FilterPlan Prepare(string rawSource, RenderContext ctx)
    {
        var hasPrivacyTags = SourceContainsPrivacyTagMarker(rawSource);
        return new FilterPlan(
            Filter: node => IsNodeVisible(node, ctx),
            Transform: (node, rendered) => Transform(node, rendered, ctx),
            HasPrivacyTags: hasPrivacyTags);
    }

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

        // Non-privacy tags are always visible.
        if (!IsPrivacySensitiveTag(tagName)) return true;

        // PlainText / EmbedSafe: strip the privacy-sensitive tag. Only [private]
        // reaches this point — [mod] is public on read and returns visible above.
        if (ctx.Audience is RenderAudience.PlainText or RenderAudience.EmbedSafe)
            return false;

        // AuthorEdit: both tags are always visible (authorship already
        // confirmed by endpoint-level authorization).
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

        var viewer = ctx.Viewer;
        var viewerId = viewer?.UserId;

        // Author-forever: the post author always sees their own [private] blocks.
        if (viewerId.HasValue && ctx.PostAuthorUserId.HasValue &&
            ctx.PostAuthorUserId.Value == viewerId.Value)
            return true;

        // Game leads (master + assistants) always see all [private] blocks.
        if (viewerId.HasValue && ContainsGuid(ctx.GameLeadUserIds, viewerId.Value))
            return true;

        // Addressee-forever: the character owner of an addressed character
        // sees the block. Resolved per-block at post save time, keyed by
        // the raw tag attribute string.
        var attribute = tagNode.AttributeValue ?? string.Empty;
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
    // TRANSFORM: no-op. The NodeTree.ToHtml transform callback fires
    // before children are emitted, so it cannot be used to augment the
    // final HTML. AuthorEdit round-trip attributes are emitted by
    // dedicated tag templates in BbParserProvider instead.
    // ───────────────────────────────────────────────────────────────────

    private static string Transform(Node node, string rendered, RenderContext ctx) => rendered;

    // ───────────────────────────────────────────────────────────────────
    // Utility: string-scan source for the [private] marker. Cheap enough at
    // source size that we do it on every render; cache lookups then skip the
    // heavy per-user buckets when the content has no [private]. [mod] is no
    // longer scanned — it renders identically for every viewer, so it never
    // forces a per-user bucket.
    // ───────────────────────────────────────────────────────────────────

    private static bool SourceContainsPrivacyTagMarker(string? source)
    {
        if (string.IsNullOrEmpty(source)) return false;
        // Case-insensitive search for "[private" followed by an attribute /
        // closing bracket. A bare "[private]" or "[private=foo]" both match.
        // Inside [noparse] blocks this over-counts, which only affects
        // bucketing (never filtering correctness).
        var span = source.AsSpan();
        for (var i = 0; i < span.Length - 3; i++)
        {
            if (span[i] != '[') continue;
            if (StartsWithIgnoreCase(span[(i + 1)..], "private"))
            {
                var next = i + 1 + "private".Length;
                if (next < span.Length && (span[next] == ']' || span[next] == '=' || span[next] == ' '))
                    return true;
            }
        }
        return false;
    }

    private static bool StartsWithIgnoreCase(ReadOnlySpan<char> span, string prefix)
    {
        if (span.Length < prefix.Length) return false;
        for (var i = 0; i < prefix.Length; i++)
        {
            var c = span[i];
            var p = prefix[i];
            if (c == p) continue;
            if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
            if (p >= 'A' && p <= 'Z') p = (char)(p + 32);
            if (c != p) return false;
        }
        return true;
    }

    private static bool ContainsGuid(IReadOnlyCollection<Guid> set, Guid value)
    {
        foreach (var item in set)
            if (item == value)
                return true;
        return false;
    }
}
