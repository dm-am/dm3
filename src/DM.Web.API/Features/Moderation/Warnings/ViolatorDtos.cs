using System;
using System.ComponentModel.DataAnnotations;
using DM.Web.API.Features.Moderation.Bans;
using DM.Web.API.Shared.Dto;
// Aliased rather than imported whole: the domain namespace carries a Violator and
// a Ban of its own, and this file declares the one and nests the other.
using WarningPolicy = DM.Domain.Moderation.Features.Warnings.WarningPolicy;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Violators list row: a user with active warning points or an active ban
/// </summary>
public class Violator
{
    /// <summary>
    /// The denominator of the "N/6" the violators table shows, and the count at
    /// which it colours the row.
    /// </summary>
    /// <remarks>
    /// The same number the automatic ban fires at, taken from the domain rather
    /// than written again here: a scale on the screen that disagreed with the line
    /// the site bans at would colour the row at one count and ban at another. The
    /// sum behind it is over every warning a user still carries, with no date
    /// filter - no window is measured anywhere, and none is described.
    /// </remarks>
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
    /// <remarks>
    /// Named after what it filters, per the query vocabulary in API_DESIGN.md.
    /// It was `filter`, which says only that the endpoint filters — a name a
    /// consumer cannot read a meaning out of and cannot guess a value for.
    /// </remarks>
    /// <example>all</example>
    [RegularExpression("^(all|banned|points-only)$",
        ErrorMessage = "Недопустимое значение фильтра")]
    public string BanState { get; set; } = "all";
}
