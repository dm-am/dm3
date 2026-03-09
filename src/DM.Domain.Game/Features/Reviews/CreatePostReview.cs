using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Reviews;

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
    /// Optional reason type for the review
    /// </summary>
    public ReviewReasonType? ReasonType { get; set; }
}
