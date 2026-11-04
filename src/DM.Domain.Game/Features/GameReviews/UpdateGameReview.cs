using System;

namespace DM.Domain.Game.Features.GameReviews;

public class UpdateGameReview
{
    public Guid ReviewId { get; set; }

    /// <summary>
    /// Null keeps the stored text
    /// </summary>
    public string? Text { get; set; }
}
