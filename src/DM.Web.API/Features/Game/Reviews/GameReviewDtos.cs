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

    /// <summary>
    /// What the viewer may do with this review, answered by the same code that
    /// refuses the request
    /// </summary>
    public GameReviewViewer Viewer { get; set; } = new();
}

/// <summary>
/// What the viewer may do with a game review. Each flag is asked by the same
/// code the write it stands for is refused by, so a page that offers by it
/// offers only what the server takes; the server refuses all the same.
/// </summary>
public class GameReviewViewer
{
    /// <summary>
    /// Whether the viewer may edit the review: its author within a day of
    /// writing it, an admin author at any time. False for a guest
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Whether the viewer may delete the review: its author or senior
    /// moderation. False for a guest
    /// </summary>
    public bool CanDelete { get; set; }
}

/// <summary>
/// Request to create a game review
/// </summary>
/// <remarks>
/// The length of the text is the domain's rule, the same for an edit and
/// counted as the text is stored. A bound of the request's own would answer
/// first, with its own number and message.
/// </remarks>
public class CreateGameReviewRequest
{
    /// <summary>
    /// Review text (up to 5000 characters, BBCode supported)
    /// </summary>
    [Required]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Request to update a game review
/// </summary>
/// <remarks>
/// The length is checked by the domain, as on creation.
/// </remarks>
public class UpdateGameReviewRequest
{
    /// <summary>
    /// New review text (up to 5000 characters, BBCode supported). Omit to keep the current one
    /// </summary>
    public string? Text { get; set; }
}
