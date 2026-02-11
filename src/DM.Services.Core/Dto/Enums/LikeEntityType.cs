namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Type of entity that can receive likes
/// </summary>
public enum LikeEntityType
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
    /// Forum topic
    /// </summary>
    Topic = 2,

    /// <summary>
    /// Site or user review
    /// </summary>
    Review = 3,

    /// <summary>
    /// Private or chat message
    /// </summary>
    Message = 4,

    /// <summary>
    /// Blog publication
    /// </summary>
    Publication = 5
}
