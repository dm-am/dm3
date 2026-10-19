using System;
using System.Collections.Generic;
using DM.Domain.Core.Authorization;
using DM.Infrastructure.Core.Parsing;

namespace DM.Web.API.Shared.BbRendering;

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
    /// <remarks>
    /// The empty id arrives here as null, the same way it does in
    /// <see cref="RenderContext.PostAuthorUserId"/>. Mapping profiles read the
    /// author off a domain model whose id nobody promised to fill, and the
    /// author id is what opens the unfiltered AuthorEdit source - so an unfilled
    /// one has to mean "no author", not "the reader who is nobody".
    /// </remarks>
    public Guid? PostAuthorUserId
    {
        get => _postAuthorUserId;
        init => _postAuthorUserId = value is { } author && !AnonymousIdentity.Is(author)
            ? author
            : null;
    }

    private readonly Guid? _postAuthorUserId;

    /// <summary>
    /// Whether the viewer may edit this text, by the same check the write of it
    /// makes. Whoever may edit a text gets its AuthorEdit source as its author
    /// does: the editor saves what it was handed, so a rendering handed to it would
    /// be written back into the text. Asked only of a viewer who is not the author
    /// and asked for the source; null for a text only its author edits.
    /// </summary>
    public Func<IIntentionManager, bool>? EditableBy { get; init; }

    /// <summary>Game the post belongs to.</summary>
    public Guid? GameId { get; init; }

    /// <summary>Per-block snapshot: raw [private] tag attribute → resolved
    /// owner user ids allowed to see that block. Populated at post save time.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<Guid>> PrivateAddresseeOwnerUserIdsByAttribute
    {
        get;
        init;
    } = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal);

    /// <summary>Per-block snapshot: raw [private] tag attribute → names of the
    /// frozen addressees, for the recipients the block carries. Populated at post save time,
    /// from the same snapshot the owner ids come from.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> PrivateAddresseeNamesByAttribute
    {
        get;
        init;
    } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>Game leads (master + assistants). Mentors are excluded.</summary>
    /// <remarks>The empty id is dropped on the way in — a lead sees every
    /// [private] block in the game, and an unfilled master id is not a lead.
    /// </remarks>
    public IReadOnlyCollection<Guid> GameLeadUserIds
    {
        get => _gameLeadUserIds;
        init => _gameLeadUserIds = AnonymousIdentity.WithoutAnonymous(value);
    }

    private readonly IReadOnlyCollection<Guid> _gameLeadUserIds = Array.Empty<Guid>();

    /// <summary>Per-game override opening addressed [private] blocks to every room reader.</summary>
    public bool GameShowPrivateMessages { get; init; }

    /// <summary>Per-room override opening addressed [private] blocks to every room reader.</summary>
    public bool RoomViewPrivateText { get; init; }

    /// <summary>
    /// Files of the post it keeps from the viewer, whose pictures and links the
    /// render cuts out (<see cref="RenderContext.WithheldUploadIds"/>).
    /// </summary>
    /// <remarks>
    /// Decided by the mapping profile for the reader the response is rendered
    /// for, by PostAttachmentVisibility, the answer the strip under the post is
    /// drawn from: the converter applies it and does not decide it again.
    /// </remarks>
    public IReadOnlySet<Guid> WithheldUploadIds { get; init; } = new HashSet<Guid>();
}
