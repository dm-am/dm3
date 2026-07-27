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
    DirectMessage = 5
}
