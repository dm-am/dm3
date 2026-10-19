using System;

namespace DM.Domain.Game.Features.GameReviews;

public class CreateGameReview
{
    public Guid GameId { get; set; }

    public string Text { get; set; } = string.Empty;
}
