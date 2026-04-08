using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Likes;

namespace DM.Domain.Game.Features.PostReviews;

/// <summary>
/// Domain DTO for post review (review of a game post with rating)
/// </summary>
/// <remarks>
/// BBCode is supported in Text.
/// Any sentiment text is allowed.
/// Has Sign (+1/0/-1) for rating impact.
/// One review per author-post pair.
/// SUPPORTS LIKES (only entity with likes in the review system).
/// </remarks>
public class PostReview : ILikable
{
    /// <summary>
    /// Review identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Author (user writing the review)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Post identifier being reviewed
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Post author
    /// </summary>
    public GeneralUser PostAuthor { get; set; } = null!;

    /// <summary>
    /// Game identifier (for filtering and navigation)
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Review text (BBCode supported, optional)
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Rating impact sign (+1, 0, -1)
    /// </summary>
    public ReviewSign Sign { get; set; }

    #region ILikable Implementation

    /// <summary>
    /// Type of likable entity
    /// </summary>
    public LikeEntityType LikeEntityType => LikeEntityType.PostReview;

    /// <summary>
    /// Users who liked this review
    /// </summary>
    public IEnumerable<GeneralUser> Likes { get; set; } = Array.Empty<GeneralUser>();

    #endregion
}
