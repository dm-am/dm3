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
    /// The total of active points at which the site bans by itself.
    /// </summary>
    /// <remarks>
    /// Also the denominator of the "N/6" the violators table shows, so the scale
    /// on the screen and the line the automatic ban is drawn at are one constant.
    /// </remarks>
    public const int AutoBanPointsThreshold = 6;

    /// <summary>
    /// How long the automatic ban runs.
    /// </summary>
    /// <remarks>
    /// Two days, and deliberately short: the automatic ban decides nothing. It
    /// buys the time in which a senior moderator or an administrator looks at
    /// what happened and either lifts it or issues a ban of his own.
    /// </remarks>
    public const int AutoBanDurationHours = 48;

    /// <summary>
    /// The scope the automatic ban is issued with: the ordinary one.
    /// </summary>
    /// <remarks>
    /// Never <see cref="AccessPolicy.FullBan" />. A full ban fails authentication
    /// itself, and locking a reader out of the site on an arithmetic threshold,
    /// before any human has looked at the case, is exactly what the two-day window
    /// exists to avoid.
    /// </remarks>
    public const AccessPolicy AutoBanPolicy = AccessPolicy.DemocraticBan;

    /// <summary>
    /// The comment the automatic ban is filed under. Read by the banned reader in
    /// the notification and by moderation in the ban list, so it says both what
    /// happened and what happens next.
    /// </summary>
    public const string AutoBanComment =
        "Автоматический бан по сумме предупреждений, до решения старшего модератора";
}
