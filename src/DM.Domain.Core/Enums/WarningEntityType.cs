namespace DM.Domain.Core.Enums;

/// <summary>
/// The places a warning names and keeps a copy of the text of: the one list of them
/// </summary>
/// <remarks>
/// Public texts of users outside game rooms and conversations, as in DM2, and only those
/// written to say something: a testimonial about the website and a recommendation are
/// kind words their subject accepts, and harassment there is warned for as in a
/// conversation. A kind left out of here is warned for with a warning to the user alone
/// (<see cref="Unknown" />), with no place and no copy: the issue refuses a kind it does
/// not find here by name.
/// </remarks>
public enum WarningEntityType
{
    /// <summary>
    /// No place: a warning to the user alone
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Forum comment
    /// </summary>
    Comment = 1,

    /// <summary>
    /// Message of the global chat, the one chat moderated
    /// </summary>
    Message = 2,

    // 3 was a game post: nothing in a game room is moderated, and the number is not reused.

    /// <summary>
    /// Forum topic
    /// </summary>
    Topic = 4,

    /// <summary>
    /// The profile of the warned user: the avatar, the status and "О себе" as
    /// they stood when the warning was issued. The entity is the warned user
    /// himself, so a warning of this kind names no place on the site.
    /// </summary>
    Profile = 5,

    /// <summary>
    /// Review of a game
    /// </summary>
    GameReview = 6,

    /// <summary>
    /// The text of a review of a post, read in the pulse and the profile
    /// </summary>
    PostReview = 7
}
