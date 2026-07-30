using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Output DTO for ban
/// </summary>
public class Ban
{
    /// <summary>
    /// Ban identifier
    /// </summary>
    public Guid BanId { get; set; }

    /// <summary>
    /// Target user identifier. Carried separately from the navigation so that
    /// authorization checks never depend on the user object being loaded.
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// Target user
    /// </summary>
    public GeneralUser TargetUser { get; set; } = null!;

    /// <summary>
    /// Ban author (moderator or user for voluntary bans)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Ban start time
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Ban end time
    /// </summary>
    public DateTimeOffset EndedUtc { get; set; }

    /// <summary>
    /// Ban comment/reason
    /// </summary>
    public string Comment { get; set; } = "";

    /// <summary>
    /// Access restriction policy
    /// </summary>
    public AccessPolicy AccessRestrictionPolicy { get; set; }

    /// <summary>
    /// Whether this is a voluntary self-ban
    /// </summary>
    public bool IsVoluntary { get; set; }

    /// <summary>
    /// Whether the ban has been removed (lifted early)
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// How far into the future a permanent ban is written. Permanence is not a
    /// column: the schema stores a window, and a window this long is what
    /// carries it.
    /// </summary>
    public const int PermanentYears = 100;

    /// <summary>
    /// The remaining lifetime above which a ban reads back as permanent.
    /// Deliberately far below <see cref="PermanentYears" />: the end date is
    /// fixed at creation while the moment it is compared against keeps moving,
    /// so an exact comparison would turn every permanent ban temporary one tick
    /// after it was issued.
    /// </summary>
    public const int PermanentThresholdYears = 50;

    /// <summary>
    /// Whether the ban is in force at the given moment.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="AccessRestriction.IsInForceAt" />, the single
    /// definition of "banned right now", so that a listing and the enforcement
    /// cannot answer differently about the same ban. A lifted ban keeps its row
    /// for the moderation history and is in force for nobody.
    /// </remarks>
    public bool IsInForceAt(DateTimeOffset moment) =>
        !IsRemoved &&
        new AccessRestriction(AccessRestrictionPolicy, StartedUtc, EndedUtc).IsInForceAt(moment);

    /// <summary>
    /// Whether the ban is a permanent one at the given moment. A voluntary
    /// self-ban never is: its length is the user's own choice, and lifting it is
    /// not reserved for administrators.
    /// </summary>
    public bool IsPermanentAt(DateTimeOffset moment) =>
        !IsVoluntary && EndedUtc > moment.AddYears(PermanentThresholdYears);
}

/// <summary>
/// Entity DTO for creating a ban (repository level)
/// </summary>
public class CreateBanEntity
{
    /// <summary>
    /// Ban identifier
    /// </summary>
    public Guid BanId { get; set; }

    /// <summary>
    /// Target user identifier
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Ban start time
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Ban end time
    /// </summary>
    public DateTimeOffset EndedUtc { get; set; }

    /// <summary>
    /// Ban comment
    /// </summary>
    public string Comment { get; set; } = "";

    /// <summary>
    /// Access restriction policy
    /// </summary>
    public AccessPolicy AccessRestrictionPolicy { get; set; }

    /// <summary>
    /// Whether this is a voluntary self-ban
    /// </summary>
    public bool IsVoluntary { get; set; }
}
