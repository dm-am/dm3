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

    /// <summary>
    /// The viewer's id, for the rules that match a reader against an id in the
    /// content. Null when the reader is anonymous.
    /// </summary>
    /// <remarks>
    /// The only id the filtering rules may read. Taking
    /// <see cref="IAuthorizationSubject.UserId"/> straight off the viewer hands
    /// the rules the empty id for a guest, and the empty id is what every field
    /// nobody filled in also holds - see <see cref="AnonymousIdentity"/>.
    /// </remarks>
    public Guid? ViewerUserId => AnonymousIdentity.Of(Viewer);

    /// <summary>Rendering intent (Display, AuthorEdit, PlainText, EmbedSafe).</summary>
    public RenderAudience Audience { get; init; }

    /// <summary>Surface the BBCode originates from; governs parse-time and
    /// defense-in-depth tag gating.</summary>
    public BbSurface Surface { get; init; }

    /// <summary>Author of the containing post (for [private] author-forever rule).</summary>
    /// <remarks>
    /// The empty id arrives here as null. A caller who leaves the author unset
    /// is saying the content has no known author, and the rule that compares a
    /// reader against it must find nothing to compare with rather than match
    /// every anonymous reader.
    /// </remarks>
    public Guid? PostAuthorUserId
    {
        get => postAuthorUserId;
        init => postAuthorUserId = value is { } author && !AnonymousIdentity.Is(author)
            ? author
            : null;
    }

    private readonly Guid? postAuthorUserId;

    /// <summary>Game the post belongs to (for lead resolution and scoping).</summary>
    public Guid? GameId { get; init; }

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

    /// <summary>Names of the frozen addressees of each [private] block, for the
    /// recipients line under it. Key = raw tag attribute value, the same key the
    /// owner ids are held under.</summary>
    /// <remarks>
    /// The line names who reads the block, and the only record of that is the
    /// snapshot. Composed from the tag attribute instead - which is what it used
    /// to be - it names whoever answers to that name today, and after a rename
    /// that is somebody who never saw the block.
    ///
    /// A key is absent when the snapshot cannot name every addressee under it,
    /// which is the whole of an old snapshot and any block that resolved to
    /// nobody. The renderer then falls back to the author's own text, which is
    /// what it has always printed.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> PrivateAddresseeNamesByAttribute
    {
        get;
        init;
    } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>Game leads (master + assistants). Mentors are NOT included.</summary>
    /// <remarks>
    /// The empty id is dropped on the way in. A lead sees every [private] block
    /// in the game, so an unfilled master id sitting in this list is the widest
    /// permission the renderer has, handed to the one reader whose own id is
    /// also empty.
    /// </remarks>
    public IReadOnlyCollection<Guid> GameLeadUserIds
    {
        get => gameLeadUserIds;
        init => gameLeadUserIds = AnonymousIdentity.WithoutAnonymous(value);
    }

    private readonly IReadOnlyCollection<Guid> gameLeadUserIds = Array.Empty<Guid>();

    /// <summary>Per-post override: when true, [private] blocks are visible to
    /// every viewer who can read the post's room.</summary>
    public bool PostSharePrivateWithAll { get; init; }

    /// <summary>Per-room override: when true, [private] blocks in any post of
    /// the room are visible to every room reader.</summary>
    public bool RoomViewPrivateText { get; init; }

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
