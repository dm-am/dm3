using System;
using System.ComponentModel.DataAnnotations;
using DM.Web.API.Features.Moderation.Bans;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Violators list row: a user with active warning points or an active ban
/// </summary>
public class Violator
{
    /// <summary>
    /// Auto-ban threshold: gaining this many points within 30 days triggers a ban
    /// </summary>
    public const int AutoBanPointsThreshold = 6;

    /// <summary>
    /// The violating user
    /// </summary>
    public UserRef User { get; set; } = null!;

    /// <summary>
    /// Current active warning points (the N in "N/6")
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Auto-ban points threshold (the 6 in "N/6")
    /// </summary>
    public int PointsThreshold { get; set; } = AutoBanPointsThreshold;

    /// <summary>
    /// Moment of the latest active warning (null if the user only has a ban)
    /// </summary>
    public DateTimeOffset? LastWarningUtc { get; set; }

    /// <summary>
    /// Active ban details (null if the user only has warning points)
    /// </summary>
    public Ban? ActiveBan { get; set; }
}

/// <summary>
/// Query parameters for the violators list
/// </summary>
public class ViolatorsQuery
{
    /// <summary>
    /// Filter: "all" (default) - points or active ban,
    /// "banned" - active ban only, "points-only" - points without a ban
    /// </summary>
    /// <example>all</example>
    [RegularExpression("^(all|banned|points-only)$",
        ErrorMessage = "Filter must be one of: all, banned, points-only")]
    public string Filter { get; set; } = "all";
}
