using System;
using DM.Web.API.Shared.Dto;
using ApiAwardType = DM.Web.API.Features.Community.Awards.AwardType;

namespace DM.Web.API.Features.Moderation.Awards;

/// <summary>
/// API DTO: an award granted within a contest series, recipient included.
/// </summary>
/// <remarks>
/// The public <see cref="Community.Awards.UserAward"/> carries no recipient —
/// it is only ever returned inside one user's list, where naming the user
/// again would be noise. A series listing is the opposite case: the recipient
/// is the point, and revoking needs it.
/// </remarks>
public class ContestSeriesAward
{
    /// <summary>Grant record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Recipient.</summary>
    public UserRef User { get; set; } = null!;

    /// <summary>Award type.</summary>
    public ApiAwardType Type { get; set; } = null!;

    /// <summary>
    /// Link to the forum topic with the work the award was granted for
    /// (optional — best_critic / guesser are not tied to a work).
    /// </summary>
    public string? WorkUrl { get; set; }

    /// <summary>Grant moment (UTC).</summary>
    public DateTimeOffset AwardedUtc { get; set; }
}
