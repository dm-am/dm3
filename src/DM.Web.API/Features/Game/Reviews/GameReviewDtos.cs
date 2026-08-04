using System;
using System.ComponentModel.DataAnnotations;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;

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
    /// Title of the reviewed game
    /// </summary>
    /// <remarks>
    /// Redundant inside one game, where the page already names it, and the only
    /// thing that identifies the row on the user-level listings: there the game
    /// is what varies from row to row, and a GUID is not a name.
    /// </remarks>
    public string? GameTitle { get; set; }

    /// <summary>
    /// Review author
    /// </summary>
    public User? Author { get; set; }

    /// <summary>
    /// Review text (BBCode supported, rendered as HTML)
    /// </summary>
    public CommonBbText? Text { get; set; }

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
