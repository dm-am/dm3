using System;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Comment in a discussion with permission flags
/// </summary>
public class DiscussionComment
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Comment author
    /// </summary>
    public User Author { get; set; } = null!;

    /// <summary>
    /// Comment text with BB code rendering
    /// </summary>
    public CommonBbText Text { get; set; } = null!;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTimeOffset? UpdatedUtc { get; set; }

    /// <summary>
    /// Number of likes on this comment
    /// </summary>
    public int LikesCount { get; set; }

    /// <summary>
    /// Whether the current user has liked this comment
    /// </summary>
    public bool IsLikedByMe { get; set; }

    /// <summary>
    /// Whether the current user can edit this comment
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Whether the current user can delete this comment
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// Whether the current user can like this comment
    /// </summary>
    public bool CanLike { get; set; }
}
