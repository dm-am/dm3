using DM.Infrastructure.Core.Parsing;

namespace DM.Web.API.Shared.BbRendering;

/// <summary>
/// BB text carrier. <see cref="Value"/> holds raw BBCode when the DTO is
/// constructed by a mapping profile; the JSON converter replaces it with
/// the rendered output (HTML / plain-text) at serialization time, using
/// <see cref="Context"/> to determine what the current viewer is allowed
/// to see. When the converter runs without a context (legacy call sites
/// that have not been migrated yet) it falls back to a public render with
/// the BbText's own <see cref="Surface"/>.
/// </summary>
public abstract class BbText
{
    /// <summary>
    /// Text
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Parse mode
    /// </summary>
    public abstract BbParseMode ParseMode { get; }

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
}

/// <inheritdoc />
public class PostBbText : BbText
{
    /// <inheritdoc />
    public override BbParseMode ParseMode => BbParseMode.Post;
    /// <inheritdoc />
    public override BbSurface Surface => BbSurface.GamePost;
}

/// <inheritdoc />
public class CommonBbText : BbText
{
    /// <inheritdoc />
    public override BbParseMode ParseMode => BbParseMode.Common;
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
    public override BbParseMode ParseMode => BbParseMode.Info;
    /// <inheritdoc />
    public override BbSurface Surface => BbSurface.Profile;
}