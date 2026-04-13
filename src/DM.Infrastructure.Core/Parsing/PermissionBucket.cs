#nullable enable
using System;
using System.Globalization;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// Equivalence class for render-cache keys. Two viewers that map to the
/// same bucket for the same source BBCode receive byte-identical output,
/// so a single cache entry serves both. Bucket selection balances hit
/// rate (coarse = more hits) against correctness (per-user when the
/// content genuinely depends on viewer identity).
/// </summary>
public readonly record struct PermissionBucket(string Key)
{
    /// <summary>Public / unauthenticated viewer. Used for content with no
    /// privacy-sensitive tags, regardless of actual viewer.</summary>
    public static readonly PermissionBucket Anonymous = new("anon");

    /// <summary>Authenticated user, content has no privacy-sensitive tags.
    /// Coarse bucket shared by every logged-in viewer for max hit rate.</summary>
    public static readonly PermissionBucket AuthenticatedCoarse = new("user");

    /// <summary>Moderator+ viewer of content containing [mod] in a surface
    /// where [mod] is valid. Coarse across all moderators.</summary>
    public static readonly PermissionBucket Moderator = new("mod");

    /// <summary>Per-user bucket for content whose [private] visibility depends
    /// on the specific viewer (neither author, lead, room-sharer nor post-sharer).</summary>
    public static PermissionBucket ForUser(Guid userId) =>
        new($"user:{userId:N}");

    /// <summary>Bucket for the author of a specific post (author-forever rule).</summary>
    public static PermissionBucket ForPostAuthor(Guid postId, Guid authorUserId) =>
        new($"author:{postId:N}:{authorUserId:N}");

    /// <summary>Bucket for game leads (master + assistants) of a specific game.</summary>
    public static PermissionBucket ForGameLead(Guid gameId) =>
        new($"lead:{gameId:N}");

    /// <summary>Bucket for a specific character owner — used for addressee
    /// visibility when content has [private] with exactly that addressee set.</summary>
    public static PermissionBucket ForAddressee(Guid ownerUserId) =>
        new($"addressee:{ownerUserId:N}");

    /// <summary>Bucket for every room reader of a room with
    /// ViewPrivateText = true. One entry serves the whole room.</summary>
    public static PermissionBucket ForRoomSharing(Guid roomId) =>
        new($"roomshare:{roomId:N}");

    /// <summary>Bucket for every room reader of a post with
    /// SharePrivateWithAll = true. One entry serves every reader of the post.</summary>
    public static PermissionBucket ForPostSharing(Guid postId) =>
        new($"postshare:{postId:N}");

    /// <summary>Bucket for AuthorEdit audience — always per-author.</summary>
    public static PermissionBucket ForAuthorEdit(Guid authorUserId) =>
        new($"authoredit:{authorUserId:N}");

    /// <summary>
    /// Compute the coarsest bucket that yields byte-identical output for the
    /// given context. Callers pass <paramref name="hasPrivacyTags"/> = false
    /// when a prior inspection of the parsed tree confirms there are no
    /// [private] / [mod] nodes, enabling maximum coarse-bucket reuse.
    /// </summary>
    public static PermissionBucket Compute(RenderContext ctx, bool hasPrivacyTags)
    {
        if (ctx.Audience == RenderAudience.PlainText || ctx.Audience == RenderAudience.EmbedSafe)
            return Anonymous;

        if (ctx.Audience == RenderAudience.AuthorEdit)
        {
            var authorId = ctx.Viewer?.UserId ?? ctx.PostAuthorUserId ?? Guid.Empty;
            return ForAuthorEdit(authorId);
        }

        // Display audience below.

        if (!hasPrivacyTags)
        {
            return ctx.Viewer is { IsAuthenticated: true } ? AuthenticatedCoarse : Anonymous;
        }

        // Content has privacy-sensitive tags — bucket depends on *why* the
        // viewer can (or cannot) see them.
        var viewerId = ctx.Viewer?.UserId;

        if (ctx.PostSharePrivateWithAll && ctx.PostId.HasValue)
            return ForPostSharing(ctx.PostId.Value);

        if (ctx.RoomViewPrivateText && ctx.RoomId.HasValue)
            return ForRoomSharing(ctx.RoomId.Value);

        if (viewerId.HasValue && ctx.PostAuthorUserId == viewerId.Value && ctx.PostId.HasValue)
            return ForPostAuthor(ctx.PostId.Value, viewerId.Value);

        if (viewerId.HasValue && Contains(ctx.GameLeadUserIds, viewerId.Value) && ctx.GameId.HasValue)
            return ForGameLead(ctx.GameId.Value);

        if (viewerId.HasValue && Contains(ctx.PrivateAddresseeOwnerUserIdsUnion, viewerId.Value))
            return ForAddressee(viewerId.Value);

        // Content has [mod] and viewer is a moderator — coarse mod bucket.
        if (ctx.Viewer is { Role: >= UserRole.Moderator })
            return Moderator;

        // Viewer does not qualify for any privacy-sensitive content — they
        // share the anonymous filtered output with every other non-privileged
        // viewer.
        return ctx.Viewer is { IsAuthenticated: true } ? AuthenticatedCoarse : Anonymous;
    }

    private static bool Contains(System.Collections.Generic.IReadOnlyCollection<Guid> set, Guid value)
    {
        foreach (var item in set)
            if (item == value)
                return true;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Key;
}
