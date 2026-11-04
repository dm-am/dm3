using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// The numbers the warning scale and the automatic ban are written with, in one
/// place because the scale the violators table draws and the count the automatic
/// ban fires at are the same number and must not drift apart.
/// </summary>
public static class WarningPolicy
{
    /// <summary>
    /// The most a single warning can cost. Zero is a verbal warning: recorded,
    /// adding nothing to the total.
    /// </summary>
    public const int MaxPointsPerWarning = 6;

    /// <summary>
    /// The total of active points at which the site bans by itself, and the
    /// denominator of the "N/6" the violators table shows.
    /// </summary>
    public const int AutoBanPointsThreshold = 6;

    /// <summary>
    /// How long the automatic ban runs: two days for a senior moderator or an
    /// administrator to look at the case and lift the ban or issue one of his own.
    /// </summary>
    public const int AutoBanDurationHours = 48;

    /// <summary>
    /// The scope the automatic ban is issued with: the ordinary one, never
    /// <see cref="AccessPolicy.FullBan" />, which fails authentication before any human
    /// has looked at the case.
    /// </summary>
    public const AccessPolicy AutoBanPolicy = AccessPolicy.DemocraticBan;

    /// <summary>
    /// The comment the automatic ban is filed under. Read by the banned reader in
    /// the notification and by moderation in the ban list, so it says both what
    /// happened and what happens next.
    /// </summary>
    public const string AutoBanComment =
        "Автоматический бан по сумме предупреждений, до решения старшего модератора";

    /// <summary>
    /// The longest text of a moderation decision: the reason of a warning, of a change
    /// of its points, of a ban and of a lift.
    /// </summary>
    /// <remarks>
    /// About a page of text: the reason travels whole in the notification the user
    /// receives and stands whole in the moderation lists, and an argument longer than
    /// that belongs in a ticket. The moderation windows hold a copy, compared by
    /// ClientFieldLimitsShould.
    /// </remarks>
    public const int ReasonMaxLength = 2000;
}
