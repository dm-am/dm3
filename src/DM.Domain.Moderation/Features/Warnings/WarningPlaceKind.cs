namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// The place the offending text stood in, as the caption of a warning names it
/// </summary>
public enum WarningPlaceKind
{
    /// <summary>
    /// A forum topic: the topic itself or a comment under it
    /// </summary>
    Topic = 1,

    /// <summary>
    /// The discussion of a game
    /// </summary>
    Game = 2,

    /// <summary>
    /// The discussion of a blog
    /// </summary>
    Blog = 3,

    /// <summary>
    /// The global chat
    /// </summary>
    GlobalChat = 4,

    /// <summary>
    /// A publication of a blog: a comment under it
    /// </summary>
    Publication = 5
}
