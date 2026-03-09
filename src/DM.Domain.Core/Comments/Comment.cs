using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Likes;

namespace DM.Domain.Core.Comments;

/// <summary>
/// Comment DTO model
/// </summary>
public class Comment : ILikable
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public LikeEntityType LikeEntityType => LikeEntityType.Comment;

    /// <summary>
    /// Parent entity identifier
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Date of creation (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Date of last modification (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Comment text (raw BBCode or markdown)
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Comment author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <inheritdoc />
    public IEnumerable<GeneralUser> Likes { get; set; } = [];
}
