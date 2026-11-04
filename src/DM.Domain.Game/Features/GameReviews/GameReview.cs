using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.GameReviews;

/// <remarks>
/// One review per author and game.
/// </remarks>
public class GameReview
{
    public Guid Id { get; set; }

    public GeneralUser Author { get; set; } = null!;

    public Guid GameId { get; set; }

    public string? GameTitle { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// BBCode
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
