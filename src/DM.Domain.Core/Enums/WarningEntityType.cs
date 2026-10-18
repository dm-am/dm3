namespace DM.Domain.Core.Enums;

/// <summary>
/// Type of entity that can receive warnings
/// </summary>
public enum WarningEntityType
{
    /// <summary>
    /// Unknown or unspecified entity type
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Forum comment
    /// </summary>
    Comment = 1,

    /// <summary>
    /// Private or chat message
    /// </summary>
    Message = 2,

    /// <summary>
    /// Game post
    /// </summary>
    Post = 3,

    /// <summary>
    /// Forum topic
    /// </summary>
    Topic = 4,

    /// <summary>
    /// The profile of the warned user: the avatar, the status and "О себе" as
    /// they stood when the warning was issued. The entity is the warned user
    /// himself, so a warning of this kind names no place on the site.
    /// </summary>
    Profile = 5
}
