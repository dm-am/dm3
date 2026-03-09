namespace DM.Domain.Core.Comments;

/// <summary>
/// Entity types that support comments
/// </summary>
public enum CommentEntityType
{
    /// <summary>Game comments</summary>
    Game = 1,

    /// <summary>Blog comments</summary>
    Blog = 2,

    /// <summary>Publication comments</summary>
    Publication = 3,

    /// <summary>Forum topic comments</summary>
    Topic = 4
}
