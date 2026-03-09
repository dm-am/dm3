using System;

namespace DM.Domain.Core.Comments;

/// <summary>
/// Request to update a comment
/// </summary>
public class UpdateComment
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Updated comment text (BBCode or markdown)
    /// </summary>
    public string? Text { get; set; }
}
