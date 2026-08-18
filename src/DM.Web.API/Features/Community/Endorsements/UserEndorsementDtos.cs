using System;
using System.ComponentModel.DataAnnotations;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Community.Endorsements;

/// <summary>
/// User endorsement information (positive recommendation)
/// </summary>
/// <remarks>
/// Endorsements are positive-only recommendations between users who have played together.
/// No likes support - this is a simple recommendation system.
/// </remarks>
public class UserEndorsement
{
    /// <summary>
    /// Endorsement unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Endorsement author details (user giving the endorsement)
    /// </summary>
    public User? Author { get; set; }

    /// <summary>
    /// Target user details (user receiving the endorsement)
    /// </summary>
    public User? TargetUser { get; set; }

    /// <summary>
    /// Endorsement text content. Plain text by contract (owner decision):
    /// no BBCode parsing or server-side HTML rendering, the client
    /// displays it as-is.
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
/// Whether the caller may write a recommendation about a given user
/// </summary>
/// <remarks>
/// The server's own answer to "may I?", asked before the control that would
/// send the POST is drawn. It is produced by the same evaluation the POST
/// refuses by, so a client that draws the control only on
/// <see cref="CanCreate" /> never offers what the create call would reject.
/// </remarks>
public class EndorsementEligibility
{
    /// <summary>
    /// Whether a recommendation about this user may be written now
    /// </summary>
    public bool CanCreate { get; set; }

    /// <summary>
    /// Why not, in the words the reader is shown. Null when
    /// <see cref="CanCreate" /> is true.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Request to create a new user endorsement
/// </summary>
public class CreateUserEndorsementRequest
{
    /// <summary>
    /// Endorsement text content (plain text, 10-5000 characters, positive only)
    /// </summary>
    [Required(ErrorMessage = "Введите текст рекомендации")]
    [StringLength(5000, MinimumLength = 10, ErrorMessage = "Рекомендация от 10 до 5000 символов")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Request to update an existing user endorsement
/// </summary>
public class UpdateUserEndorsementRequest
{
    /// <summary>
    /// Updated endorsement text (plain text, 10-5000 characters, positive only)
    /// </summary>
    [StringLength(5000, MinimumLength = 10, ErrorMessage = "Рекомендация от 10 до 5000 символов")]
    public string? Text { get; set; }
}
