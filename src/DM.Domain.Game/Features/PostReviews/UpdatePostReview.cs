using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.PostReviews;

/// <summary>
/// Input DTO for updating a post review
/// </summary>
public class UpdatePostReview
{
    /// <summary>
    /// Review ID to update
    /// </summary>
    public Guid ReviewId { get; set; }

    /// <summary>
    /// New review sign (null to keep current)
    /// </summary>
    public ReviewSign? Sign { get; set; }
}
