using System;
using System.ComponentModel.DataAnnotations;
using DM.Web.API.Features.Moderation.Bans;
using DM.Web.API.Shared.Dto;
using WarningPolicy = DM.Domain.Moderation.Features.Warnings.WarningPolicy;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Violators list row: a user with active warning points or an active ban
/// </summary>
public class Violator
{
    /// <summary>
    /// The denominator the violators table draws points against and colours the row
    /// at: the count the automatic ban fires at.
    /// </summary>
    public const int WarningPointsScale = WarningPolicy.AutoBanPointsThreshold;

    /// <summary>
    /// The violating user
    /// </summary>
    public UserRef User { get; set; } = null!;

    /// <summary>
    /// Current active warning points (the N in "N/6")
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// The 6 in "N/6" — the scale the table draws points against, not a limit
    /// that does anything by itself
    /// </summary>
    public int PointsThreshold { get; set; } = WarningPointsScale;

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
    /// Ban state: "all" (default) - points or active ban,
    /// "banned" - active ban only, "points-only" - points without a ban
    /// </summary>
    /// <example>all</example>
    [RegularExpression("^(all|banned|points-only)$",
        ErrorMessage = "Недопустимое значение фильтра")]
    public string BanState { get; set; } = "all";
}
