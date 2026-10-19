namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// Where a BBCode string comes from, which decides its tag set.
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
    /// The description of a game tag, shown in its tooltip: [tip] alone, none of
    /// the tags of a post. One parser for every audience.
    /// </summary>
    Hint = 6
}
