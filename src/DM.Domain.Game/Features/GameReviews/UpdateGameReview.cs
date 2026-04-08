using System;

namespace DM.Domain.Game.Features.GameReviews;

/// <summary>
/// Input DTO for updating a game review
/// </summary>
public class UpdateGameReview
{
    /// <summary>
    /// Review ID to update
    /// </summary>
    public Guid ReviewId { get; set; }

    /// <summary>
    /// New review text content
    /// </summary>
    public string? Text { get; set; }
}
