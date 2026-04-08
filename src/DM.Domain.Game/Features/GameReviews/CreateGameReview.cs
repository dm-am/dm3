using System;

namespace DM.Domain.Game.Features.GameReviews;

/// <summary>
/// Input DTO for creating a game review
/// </summary>
public class CreateGameReview
{
    /// <summary>
    /// Target game ID to review
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Review text content
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
