using System;

namespace DM.Domain.Game.Features.GameReviews;

public class CreateGameReviewEntity
{
    public Guid ReviewId { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    public Guid UserId { get; set; }

    public Guid GameId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public required string Text { get; set; }
}

/// <summary>
/// A null field is left unchanged
/// </summary>
public record UpdateGameReviewEntity(
    Guid ReviewId,
    string? Text = null,
    bool? IsRemoved = null,
    DateTimeOffset? ModifiedUtc = null,
    Guid? ModifiedByUserId = null,
    DateTimeOffset? DeletedUtc = null,
    Guid? DeletedByUserId = null);
