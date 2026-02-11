using System;
using System.Collections.Generic;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Common.Dto;

/// <summary>
/// DTO model for comment
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
    /// Text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <inheritdoc />
    public IEnumerable<GeneralUser> Likes { get; set; } = [];
}