using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Per-user aggregate of active warning points (repository level)
/// </summary>
public class UserWarningSummary
{
    public GeneralUser User { get; set; } = null!;

    /// <summary>
    /// Sum of active (non-removed) warning points
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Moment of the latest active warning
    /// </summary>
    public DateTimeOffset LastWarningUtc { get; set; }
}
