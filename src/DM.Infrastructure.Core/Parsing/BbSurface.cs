namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// Semantic surface a BBCode string originates from. Each surface has
/// a distinct allowed tag set, enforced at parse time.
/// </summary>
public enum BbSurface
{
    /// <summary>
    /// Game post. Allows [private] with character addressees, no [mod].
    /// </summary>
    GamePost = 0,

    /// <summary>
    /// Any comment or topic body (forum, blog, game, etc.) that uses the
    /// shared Comment entity. Allows [mod], no [private].
    /// </summary>
    Comment = 2,

    /// <summary>
    /// Global chat message. Allows [mod], no [private].
    /// </summary>
    GlobalChatMessage = 3,

    /// <summary>
    /// User profile text (bio, best post, etc.). Neither [mod] nor [private].
    /// </summary>
    Profile = 4,

    /// <summary>
    /// Direct (1-to-1) message. Neither [mod] nor [private].
    /// </summary>
    DirectMessage = 5,

    /// <summary>
    /// The description of a game tag, shown in the tag's tooltip. Not a surface
    /// of content: its one tag is [tip], a word that shows a text or a picture
    /// of its own when a reader points at it, and none of the tags of a post is
    /// markup here. [noparse] shows an example as typed, as everywhere, and
    /// [private] is read and erased for every reader, as on every surface that
    /// does not declare it; the save path refuses it. Every audience reads it
    /// with the same parser: a moderator edits the raw string, so there is no
    /// author's variant, and the safe one would add the tags of a post.
    /// </summary>
    Hint = 6
}
