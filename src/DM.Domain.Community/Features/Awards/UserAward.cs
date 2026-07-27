using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// An award granted to a specific user within a contest series.
/// Note and AwardedBy are not exposed in the public API — the description already lives on the type,
/// the granting moderator is stored for DB-level audit but is not returned to the client.
/// </summary>
public class UserAward
{
    /// <summary>Grant record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>User the award was granted to.</summary>
    public GeneralUser User { get; set; } = null!;

    /// <summary>Award type (a catalog record).</summary>
    public AwardType Type { get; set; } = null!;

    /// <summary>Contest series the award was granted within (optional).</summary>
    public ContestSeries? ContestSeries { get; set; }

    /// <summary>
    /// Link to the forum topic with the work the award was granted for.
    /// Optional — best_critic / guesser are not tied to a specific work.
    /// </summary>
    public string? WorkUrl { get; set; }

    /// <summary>Grant moment (UTC).</summary>
    public DateTimeOffset AwardedUtc { get; set; }
}

/// <summary>Request to grant an award to a user.</summary>
public class CreateUserAward
{
    /// <summary>Recipient.</summary>
    public Guid UserId { get; set; }
    /// <summary>Award type from the catalog.</summary>
    public Guid AwardTypeId { get; set; }
    /// <summary>Contest series (optional, for future out-of-contest awards).</summary>
    public Guid? ContestSeriesId { get; set; }
    /// <summary>Link to the topic with the work (optional).</summary>
    public string? WorkUrl { get; set; }
}
