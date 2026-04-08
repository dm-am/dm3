using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.PostReviews;

/// <summary>
/// Input DTO for creating a post review
/// </summary>
public class CreatePostReview
{
    /// <summary>
    /// Target post ID to review
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Review sentiment sign (positive, negative, neutral)
    /// </summary>
    public ReviewSign Sign { get; set; }

    /// <summary>
    /// Review text (required, BBCode supported)
    /// </summary>
    public string Text { get; set; } = null!;
}
