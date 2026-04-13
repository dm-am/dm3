#nullable enable
using System;
using System.Collections.Generic;
using DM.Infrastructure.Core.Parsing;

namespace DM.Web.API.Shared.BbRendering;

#pragma warning disable CS1591

/// <summary>
/// API-layer carrier of BBCode content provenance. Mapping profiles
/// populate this envelope at the boundary that knows where the content
/// came from. The BbConverter then combines it with the current viewer
/// to produce a full <see cref="RenderContext"/> at serialization time.
/// Viewer is NOT part of the envelope — it is resolved per-request.
/// </summary>
public sealed class RenderContextEnvelope
{
    /// <summary>Semantic surface the content originates from.</summary>
    public BbSurface Surface { get; init; }

    /// <summary>Author of the containing post (for [private] author-forever).</summary>
    public Guid? PostAuthorUserId { get; init; }

    /// <summary>Post identifier (for bucket scoping when SharePrivateWithAll).</summary>
    public Guid? PostId { get; init; }

    /// <summary>Game the post belongs to.</summary>
    public Guid? GameId { get; init; }

    /// <summary>Room the post belongs to.</summary>
    public Guid? RoomId { get; init; }

    /// <summary>Per-block snapshot: raw [private] tag attribute → resolved
    /// owner user ids allowed to see that block. Populated at post save time.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<Guid>> PrivateAddresseeOwnerUserIdsByAttribute
    {
        get;
        init;
    } = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal);

    /// <summary>Game leads (master + assistants). Mentors are excluded.</summary>
    public IReadOnlyCollection<Guid> GameLeadUserIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Per-post override opening [private] to every room reader.</summary>
    public bool PostSharePrivateWithAll { get; init; }

    /// <summary>Per-room override opening [private] to every room reader.</summary>
    public bool RoomViewPrivateText { get; init; }

    /// <summary>Minimal envelope for surfaces with no provenance fields
    /// (profile bio, direct message).</summary>
    public static RenderContextEnvelope ForSurface(BbSurface surface) => new() { Surface = surface };
}
