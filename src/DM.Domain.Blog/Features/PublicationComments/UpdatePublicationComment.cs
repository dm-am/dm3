using System;

namespace DM.Domain.Blog.Features.PublicationComments;

/// <summary>
/// DTO for updating a publication comment
/// </summary>
public class UpdatePublicationComment
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Updated comment text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Last edit timestamp
    /// </summary>
    public DateTimeOffset LastUpdateUtc { get; set; }
}
