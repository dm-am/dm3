using System;
using System.Collections.Generic;
using DM.Services.Common.Dto;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Forum.Dto.Output;

/// <summary>
/// Topic DTO model
/// </summary>
public class Topic : ILikable
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public LikeEntityType LikeEntityType => LikeEntityType.Topic;

    /// <summary>
    /// Board
    /// </summary>
    public Board Board { get; set; } = null!;

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Total comments count
    /// </summary>
    public int TotalCommentsCount { get; set; }

    /// <summary>
    /// Unread comments count
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    /// <summary>
    /// Last comment
    /// </summary>
    public LastComment LastComment { get; set; } = null!;

    /// <summary>
    /// Attached
    /// </summary>
    public bool IsAttached { get; set; }

    /// <summary>
    /// Closed
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Last commentary creation moment or (if none) topic creation moment (UTC)
    /// </summary>
    public DateTimeOffset LastActivityUtc { get; set; }

    /// <inheritdoc />
    public IEnumerable<GeneralUser> Likes { get; set; } = [];
}

/// <summary>
/// Last commentary DTO model
/// </summary>
public class LastComment
{
    /// <summary>
    /// Author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}