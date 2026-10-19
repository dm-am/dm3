using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Bans;

public class CreateBan
{
    public string Username { get; set; } = "";

    public DateTimeOffset? ExpiresUtc { get; set; }

    /// <summary>
    /// Ban duration in hours (alternative to ExpiresUtc)
    /// </summary>
    public int? DurationHours { get; set; }

    public string Comment { get; set; } = "";

    /// <summary>
    /// <see cref="AccessPolicy.DemocraticBan"/> or <see cref="AccessPolicy.FullBan"/>, what
    /// either forbids being stated on those members; anything else is refused with 400.
    /// </summary>
    public AccessPolicy AccessRestrictionPolicy { get; set; } = AccessPolicy.FullBan;

    /// <summary>
    /// Whether this is a voluntary self-ban
    /// </summary>
    public bool IsVoluntary { get; set; }
}
