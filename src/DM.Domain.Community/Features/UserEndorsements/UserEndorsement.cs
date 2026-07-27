using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Domain DTO for user endorsement (positive recommendation of a user)
/// </summary>
/// <remarks>
/// Text is plain text (owner decision) - no BBCode rendering.
/// Only positive text is allowed.
/// One endorsement per author-target pair.
/// NO likes support.
/// </remarks>
public class UserEndorsement
{
    /// <summary>
    /// Endorsement identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Author (user writing the endorsement)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Target user being endorsed
    /// </summary>
    public GeneralUser TargetUser { get; set; } = null!;

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Endorsement text (plain text, positive only)
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
