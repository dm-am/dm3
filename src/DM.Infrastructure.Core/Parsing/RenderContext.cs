#nullable enable
using System;
using System.Collections.Generic;
using DM.Domain.Core.Authorization;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// Everything the renderer needs to produce permission-correct HTML/text
/// for a single BBCode string. Immutable; constructed by mapping layer
/// at the boundary that knows the content provenance, then threaded
/// into the visitor.
/// </summary>
public sealed record RenderContext
{
    /// <summary>Viewer executing the render. Null only for PlainText audience.</summary>
    public IAuthorizationSubject? Viewer { get; init; }

    /// <summary>Rendering intent (Display, AuthorEdit, PlainText, EmbedSafe).</summary>
    public RenderAudience Audience { get; init; }

    /// <summary>Surface the BBCode originates from; governs parse-time and
    /// defense-in-depth tag gating.</summary>
    public BbSurface Surface { get; init; }

    /// <summary>Author of the containing post (for [private] author-forever rule).</summary>
    public Guid? PostAuthorUserId { get; init; }

    /// <summary>Game the post belongs to (for lead resolution and scoping).</summary>
    public Guid? GameId { get; init; }

    /// <summary>Room the post belongs to (for bucket key scoping).</summary>
    public Guid? RoomId { get; init; }

    /// <summary>Post identifier (for per-post bucket scoping when SharePrivateWithAll).</summary>
    public Guid? PostId { get; init; }

    /// <summary>Per-block snapshot of character-owner user ids allowed to see each
    /// [private] block in the content. Key = raw tag attribute value (e.g. the
    /// character-name list the author wrote); value = resolved owner user ids at
    /// post save time. Empty dictionary = content has no [private] blocks or the
    /// surface disallows them.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<Guid>> PrivateAddresseeOwnerUserIdsByAttribute
    {
        get;
        init;
    } = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal);

    /// <summary>Flat union of every allowed owner user id across every [private]
    /// block; used for bucket computation only (bucketing is coarser than the
    /// per-block visibility check).</summary>
    public IReadOnlyCollection<Guid> PrivateAddresseeOwnerUserIdsUnion
    {
        get
        {
            if (PrivateAddresseeOwnerUserIdsByAttribute.Count == 0) return Array.Empty<Guid>();
            var set = new HashSet<Guid>();
            foreach (var kvp in PrivateAddresseeOwnerUserIdsByAttribute)
                foreach (var userId in kvp.Value)
                    set.Add(userId);
            return set;
        }
    }

    /// <summary>Game leads (master + assistants). Mentors are NOT included.</summary>
    public IReadOnlyCollection<Guid> GameLeadUserIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Per-post override: when true, [private] blocks are visible to
    /// every viewer who can read the post's room.</summary>
    public bool PostSharePrivateWithAll { get; init; }

    /// <summary>Per-room override: when true, [private] blocks in any post of
    /// the room are visible to every room reader.</summary>
    public bool RoomViewPrivateText { get; init; }

    /// <summary>Context for an anonymous public render (no privacy tags will be
    /// visible even if surface allowed them).</summary>
    public static RenderContext ForAnonymousDisplay(BbSurface surface) => new()
    {
        Viewer = null,
        Audience = RenderAudience.Display,
        Surface = surface
    };

    /// <summary>Context for an author editing their own content. Endpoint-level
    /// authorization must have confirmed authorship before this is used.</summary>
    public static RenderContext ForAuthorEdit(IAuthorizationSubject author, BbSurface surface) => new()
    {
        Viewer = author,
        Audience = RenderAudience.AuthorEdit,
        Surface = surface,
        PostAuthorUserId = author.UserId
    };

    /// <summary>Context for a plain-text channel (email, notification digest).
    /// Privacy tags are unconditionally stripped regardless of other fields.</summary>
    public static RenderContext ForPlainText() => new()
    {
        Viewer = null,
        Audience = RenderAudience.PlainText,
        Surface = BbSurface.Profile
    };

    /// <summary>Context for an embed-safe render (link preview, cross-post embed).</summary>
    public static RenderContext ForEmbedSafe(BbSurface surface) => new()
    {
        Viewer = null,
        Audience = RenderAudience.EmbedSafe,
        Surface = surface
    };
}
