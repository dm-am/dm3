using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.GameReviews;

/// <summary>
/// Domain DTO for game review (review of a game by a player)
/// </summary>
/// <remarks>
/// BBCode is supported in Text.
/// Any sentiment text is allowed (positive, negative, neutral).
/// One review per author-game pair.
/// NO likes support.
/// </remarks>
public class GameReview
{
    /// <summary>
    /// Review identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Author (user writing the review)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Game identifier being reviewed
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game title (for display purposes)
    /// </summary>
    public string? GameTitle { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Review text (BBCode supported)
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
