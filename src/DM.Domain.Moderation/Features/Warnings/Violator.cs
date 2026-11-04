using System;
using DM.Domain.Core.Dto;
using DM.Domain.Moderation.Features.Bans;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Output DTO for a violator: a user with active warning points or an active ban
/// </summary>
public class Violator
{
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
