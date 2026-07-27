using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Filter for the violators list
/// </summary>
public enum ViolatorsFilter
{
    /// <summary>
    /// All users with active warning points or an active ban
    /// </summary>
    All,

    /// <summary>
    /// Only users with an active ban
    /// </summary>
    Banned,

    /// <summary>
    /// Only users with active warning points and no active ban
    /// </summary>
    PointsOnly
}

/// <summary>
/// Output DTO for a violator: a user with active warning points or an active ban
/// </summary>
public class Violator
{
    /// <summary>
    /// The violating user
    /// </summary>
    public GeneralUser User { get; set; } = null!;

    /// <summary>
    /// Current active warning points
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Moment of the latest active warning (null if the user only has a ban)
    /// </summary>
    public DateTimeOffset? LastWarningUtc { get; set; }

    /// <summary>
    /// Active ban (null if the user only has warning points)
    /// </summary>
    public Ban? ActiveBan { get; set; }
}

/// <summary>
/// Per-user aggregate of active warning points (repository level)
/// </summary>
public class UserWarningSummary
{
    /// <summary>
    /// The user holding the points
    /// </summary>
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
