using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Likes;
using DM.Domain.Forum.Features.Boards;

namespace DM.Domain.Forum.Features.Topics;

public class Topic : ILikable
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// Topic number within board (for URL, stable)
    /// </summary>
    public int TopicNumber { get; set; }

    /// <inheritdoc />
    public LikeEntityType LikeEntityType => LikeEntityType.Topic;

    public Board Board { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Text { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public GeneralUser Author { get; set; } = null!;

    public int TotalCommentsCount { get; set; }

    public int UnreadCommentsCount { get; set; }

    public LastComment LastComment { get; set; } = null!;

    public bool IsAttached { get; set; }

    /// <summary>
    /// Sort order for attached topics (0 = first, null = default order)
    /// </summary>
    public int? AttachOrder { get; set; }

    public bool IsClosed { get; set; }

    /// <summary>
    /// Closed by its author, who may then reopen it; a topic closed by a moderator
    /// or an administrator stays closed to the author
    /// </summary>
    public bool ClosedByAuthor { get; set; }

    /// <summary>
    /// Last comment creation moment or (if none) topic creation moment (UTC)
    /// </summary>
    public DateTimeOffset LastActivityUtc { get; set; }

    /// <inheritdoc />
    public IEnumerable<GeneralUser> Likes { get; set; } = [];

    /// <summary>
    /// Filled in the list path by one batched GROUP BY, never an inline correlated count
    /// </summary>
    public int LikesCount { get; set; }
}

public class LastComment
{
    public Guid Id { get; set; }

    public GeneralUser Author { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }
}
