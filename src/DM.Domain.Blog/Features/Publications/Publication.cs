using System;
using System.Collections.Generic;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Likes;

namespace DM.Domain.Blog.Features.Publications;

public class Publication : ILikable
{
    public Guid Id { get; set; }

    /// <inheritdoc />
    public LikeEntityType LikeEntityType => LikeEntityType.Publication;

    public Guid BlogId { get; set; }

    /// <summary>
    /// Parent blog title (projected via join — lets callers show the blog
    /// name instead of a bare blog id, e.g. the profile "best publication").
    /// </summary>
    public string BlogTitle { get; set; } = null!;

    public Rubric? Rubric { get; set; }

    public GeneralUser Author { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    /// <summary>
    /// Short preview/excerpt
    /// </summary>
    public string Preview { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public int ViewCount { get; set; }

    public int CommentCount { get; set; }

    public int UnreadCommentsCount { get; set; }

    public Guid? LastCommentId { get; set; }

    /// <summary>
    /// Users who liked this publication
    /// </summary>
    public IEnumerable<GeneralUser> Likes { get; set; } = [];
}
