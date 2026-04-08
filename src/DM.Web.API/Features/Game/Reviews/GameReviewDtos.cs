using System;
using System.ComponentModel.DataAnnotations;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// Game review DTO
/// </summary>
public class GameReviewDto
{
    /// <summary>
    /// Review unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Review author
    /// </summary>
    public User? Author { get; set; }

    /// <summary>
    /// Review text (BBCode supported)
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification timestamp (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }
}

/// <summary>
/// Request to create a game review
/// </summary>
public class CreateGameReviewRequest
{
    /// <summary>
    /// Review text (10-10000 characters, BBCode supported)
    /// </summary>
    [Required]
    [StringLength(10000, MinimumLength = 10, ErrorMessage = "Review text must be between 10 and 10000 characters")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Request to update a game review
/// </summary>
public class UpdateGameReviewRequest
{
    /// <summary>
    /// Updated review text (BBCode supported)
    /// </summary>
    [StringLength(10000, ErrorMessage = "Review text must not exceed 10000 characters")]
    public string? Text { get; set; }
}
