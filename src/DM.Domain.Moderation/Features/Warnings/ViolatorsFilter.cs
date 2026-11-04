namespace DM.Domain.Moderation.Features.Warnings;

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
