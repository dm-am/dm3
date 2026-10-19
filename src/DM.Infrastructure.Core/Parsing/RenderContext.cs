using System;
using System.Collections.Generic;
using DM.Domain.Core.Authorization;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// What the visibility filter needs to render one BBCode string.
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
    /// The only id the rules may read: a guest carries the empty id, as does every
    /// field nobody filled in (<see cref="AnonymousIdentity"/>).
    /// </remarks>
    public Guid? ViewerUserId => AnonymousIdentity.Of(Viewer);

    /// <summary>Rendering intent (Display, AuthorEdit, PlainText, EmbedSafe).</summary>
    public RenderAudience Audience { get; init; }

    /// <summary>Surface the BBCode originates from; governs parse-time and
    /// defense-in-depth tag gating.</summary>
    public BbSurface Surface { get; init; }

    /// <summary>Author of the containing post (for [private] author-forever rule).</summary>
    /// <remarks>
    /// The empty id is stored as null, so it matches no anonymous reader.
    /// </remarks>
    public Guid? PostAuthorUserId
    {
        get => _postAuthorUserId;
        init => _postAuthorUserId = value is { } author && !AnonymousIdentity.Is(author)
            ? author
            : null;
    }

    private readonly Guid? _postAuthorUserId;

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
    /// recipients it carries. Key = raw tag attribute value, the same key the
    /// owner ids are held under.</summary>
    /// <remarks>
    /// The attribute names whoever answers to the name today, the snapshot the one
    /// who reads the block. A key is absent when the snapshot cannot name every
    /// addressee, and the author's text is shown instead.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> PrivateAddresseeNamesByAttribute
    {
        get;
        init;
    } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>Game leads (master + assistants). Mentors are NOT included.</summary>
    /// <remarks>
    /// The empty id is dropped, or a guest would read every [private] block as a lead.
    /// </remarks>
    public IReadOnlyCollection<Guid> GameLeadUserIds
    {
        get => _gameLeadUserIds;
        init => _gameLeadUserIds = AnonymousIdentity.WithoutAnonymous(value);
    }

    private readonly IReadOnlyCollection<Guid> _gameLeadUserIds = Array.Empty<Guid>();

    /// <summary>Per-game override, "Показывать всем скрытые блоки": when true,
    /// the addressed [private] blocks of every post of the game are visible to
    /// every reader of its room.</summary>
    public bool GameShowPrivateMessages { get; init; }

    /// <summary>Per-room override: when true, [private] blocks in any post of
    /// the room are visible to every room reader.</summary>
    public bool RoomViewPrivateText { get; init; }

    /// <summary>
    /// Files of the post the viewer does not read: an [img] or a [link] whose
    /// address names one is cut out, without trace, as a block he does not read.
    /// </summary>
    /// <remarks>
    /// The page shows nothing where the picture stood, rather than a broken one
    /// that says a file is there and is not his.
    /// </remarks>
    public IReadOnlySet<Guid> WithheldUploadIds { get; init; } = new HashSet<Guid>();

    /// <summary>
    /// Whether every [img] and [link] whose address holds an identifier is cut out,
    /// for a reader nobody knows: the text of the search index, written once for
    /// every reader.
    /// </summary>
    /// <remarks>
    /// Which files a reader does not read is decided by the reader, and the index
    /// has none, so it keeps the address of no file at all. An identifier in an
    /// address of another site loses its picture or link from the index with it,
    /// which costs a search by address and nothing else.
    /// </remarks>
    public bool WithholdsEveryUpload { get; init; }

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
