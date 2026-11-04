using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Positive recommendation of a user, one per author-target pair
/// </summary>
/// <remarks>
/// Plain text, no BBCode rendering, and no likes.
/// </remarks>
public class UserEndorsement
{
    public Guid Id { get; set; }

    /// <summary>
    /// Author (user writing the endorsement)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Target user being endorsed
    /// </summary>
    public GeneralUser TargetUser { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Plain text, positive only
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
