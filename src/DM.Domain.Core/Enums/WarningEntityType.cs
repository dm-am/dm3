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
    Topic = 4
}
