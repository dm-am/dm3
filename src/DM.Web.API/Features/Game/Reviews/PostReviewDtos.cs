using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// post review DTO
/// </summary>
public class PostReviewDto
{
    /// <summary>
    /// Review unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Post identifier being reviewed
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Game identifier (denormalized)
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Review author
    /// </summary>
    public User? Author { get; set; }

    /// <summary>
    /// Post author (recipient of the review)
    /// </summary>
    public User? PostAuthor { get; set; }

    /// <summary>
    /// Review text (BBCode supported, optional, rendered as HTML)
    /// </summary>
    public CommonBbText? Text { get; set; }

    /// <summary>
    /// Rating sign (+1, 0, -1)
    /// </summary>
    public ReviewSign Sign { get; set; }

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification timestamp (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Users who liked this review
    /// </summary>
    public IEnumerable<User> Likes { get; set; } = [];
}

/// <summary>
/// Request to create a post review
/// </summary>
public class CreatePostReviewRequest
{
    /// <summary>
    /// Rating sign (required): Positive, Neutral, or Negative
    /// </summary>
    [Required(ErrorMessage = "Sign is required")]
    public ReviewSign Sign { get; set; }

    /// <summary>
    /// Review text (required, BBCode supported)
    /// </summary>
    [Required(ErrorMessage = "Text is required")]
    public string Text { get; set; } = null!;
}

/// <summary>
/// Request to update a post review
/// </summary>
public class UpdatePostReviewRequest
{
    /// <summary>
    /// Updated rating sign
    /// </summary>
    public ReviewSign? Sign { get; set; }
}
