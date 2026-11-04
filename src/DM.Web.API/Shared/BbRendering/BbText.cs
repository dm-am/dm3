using System;
using DM.Domain.Core.Authorization;
using DM.Infrastructure.Core.Parsing;

namespace DM.Web.API.Shared.BbRendering;

/// <summary>
/// BB text carrier. <see cref="Value"/> holds raw BBCode when the DTO is
/// constructed by a mapping profile; the JSON converter replaces it with
/// the rendered output (HTML / plain-text) at serialization time, using
/// <see cref="Context"/> to determine what the current viewer is allowed
/// to see. A carrier without a context is rendered publicly, with the
/// BbText's own <see cref="Surface"/>.
/// </summary>
public abstract class BbText
{
    /// <summary>
    /// Text
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Semantic surface the text originates from. Derived classes set this
    /// as a hard default; mapping profiles may override via
    /// <see cref="Context"/>.
    /// </summary>
    public abstract BbSurface Surface { get; }

    /// <summary>
    /// Provenance envelope populated by the mapping profile. When null the
    /// converter treats the content as public (no post author, no addressees,
    /// no per-post/room overrides).
    /// </summary>
    public RenderContextEnvelope? Context { get; set; }

    /// <summary>
    /// Whether the text is rendered for the audience the request asks for
    /// (X-Dm-Audience). A text that is not is rendered for display whatever
    /// is asked. Internal, so the published contract of every carrier stays
    /// the three properties above.
    /// </summary>
    internal virtual bool FollowsAudience => true;

    /// <summary>
    /// Names the author of the text, who gets its AuthorEdit source, and the check
    /// that gives it to anybody else who may edit the text
    /// (<see cref="RenderContextEnvelope.EditableBy" />).
    /// </summary>
    internal void BindAuthor(Guid? authorUserId, Func<IIntentionManager, bool>? editableBy = null) =>
        Context = new RenderContextEnvelope
        {
            Surface = Surface,
            PostAuthorUserId = authorUserId,
            EditableBy = editableBy
        };
}

/// <inheritdoc />
public class PostBbText : BbText
{
    /// <inheritdoc />
    public override BbSurface Surface => BbSurface.GamePost;
}

/// <inheritdoc />
public class CommonBbText : BbText
{
    /// <inheritdoc />
    public override BbSurface Surface => BbSurface.Comment;
}

/// <summary>
/// Global chat message text. Uses the public real-time surface whose parser
/// wraps images in a spoiler-gated <c>SafeImage</c> so untrusted messages
/// cannot auto-embed hotlinked/NSFW images. Derives from
/// <see cref="CommonBbText"/> so it is assignable to shared message DTO
/// fields; only the <see cref="Surface"/> differs.
/// </summary>
public class GlobalChatBbText : CommonBbText
{
    /// <inheritdoc />
    public override BbSurface Surface => BbSurface.GlobalChatMessage;
}

/// <summary>
/// Direct (1-to-1) and group message text. Uses the DirectMessage surface,
/// whose parser allows neither [mod] nor [private]: private chats have no
/// moderation, so a [mod] block would be a moderator-impersonation vector.
/// Derives from <see cref="CommonBbText"/> so it stays assignable to shared
/// message DTO fields; only the <see cref="Surface"/> differs.
/// </summary>
public class DirectMessageBbText : CommonBbText
{
    /// <inheritdoc />
    public override BbSurface Surface => BbSurface.DirectMessage;
}

/// <inheritdoc />
public class InfoBbText : BbText
{
    /// <inheritdoc />
    public override BbSurface Surface => BbSurface.Profile;
}

/// <summary>
/// The description of a game tag, shown in the tag's tooltip: the hint surface,
/// whose one tag is [tip]. Rendered by the server like every text, so the
/// client puts nothing into the page but the server's markup.
/// </summary>
public class HintBbText : BbText
{
    /// <inheritdoc />
    public override BbSurface Surface => BbSurface.Hint;

    /// <inheritdoc />
    /// <remarks>
    /// The catalogue of tags is cached publicly without Vary, and a public
    /// answer is the same bytes for every value of X-Dm-Audience (API_DESIGN.md,
    /// "Кеширование ответов"): the copy stored for one audience is handed to
    /// every other. A description is drawn in a tooltip and nowhere else, so
    /// it is rendered for display on every request.
    /// </remarks>
    internal override bool FollowsAudience => false;
}

/// <summary>
/// The description of a game tag in the moderation catalogue: the hint surface,
/// rendered for the card of the tag and given as typed to the moderator who
/// opens it to edit (author_edit), unlike <see cref="HintBbText"/>.
/// </summary>
/// <remarks>
/// It may follow the audience because the catalogue is answered to a senior
/// moderator alone and is never cached publicly.
/// </remarks>
public class TagDescriptionBbText : BbText
{
    /// <inheritdoc />
    public override BbSurface Surface => BbSurface.Hint;
}
