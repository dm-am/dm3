using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;

namespace DM.Domain.Moderation.Features.Bans;

public class Ban
{
    public Guid BanId { get; set; }

    /// <summary>
    /// Target user identifier. Carried separately from the navigation so that
    /// authorization checks never depend on the user object being loaded.
    /// </summary>
    public Guid TargetUserId { get; set; }

    public GeneralUser TargetUser { get; set; } = null!;

    /// <summary>
    /// Ban author (moderator or user for voluntary bans)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    public DateTimeOffset StartedUtc { get; set; }

    public DateTimeOffset EndedUtc { get; set; }

    /// <summary>
    /// Why the ban was issued
    /// </summary>
    public string Comment { get; set; } = "";

    public AccessPolicy AccessRestrictionPolicy { get; set; }

    /// <summary>
    /// Whether this is a voluntary self-ban
    /// </summary>
    public bool IsVoluntary { get; set; }

    /// <summary>
    /// When the ban was lifted early, null while it runs its course
    /// </summary>
    public DateTimeOffset? LiftedUtc { get; set; }

    public Guid? LiftedByUserId { get; set; }

    /// <summary>
    /// The moderator who lifted the ban early, null while it runs its course or
    /// when that account is gone. Read by the ban lists on top of the row: the
    /// column holds an identifier with no navigation behind it.
    /// </summary>
    public GeneralUser? LiftedBy { get; set; }

    /// <summary>
    /// Why the ban was lifted early
    /// </summary>
    public string? LiftReason { get; set; }

    /// <summary>
    /// Whether the ban was lifted before its end date; the row stays
    /// </summary>
    public bool IsLifted => LiftedUtc.HasValue;

    /// <summary>
    /// Whether the reader of the list may lift this ban. Derived per reader by
    /// the rule that refuses the lift itself (BanService), never stored.
    /// </summary>
    public bool CanLift { get; set; }

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
        !IsLifted &&
        new AccessRestriction(AccessRestrictionPolicy, StartedUtc, EndedUtc).IsInForceAt(moment);

    /// <summary>
    /// Whether the ban is a permanent one at the given moment. A voluntary
    /// self-ban never is: its length is the user's own choice, and lifting it is
    /// not reserved for administrators.
    /// </summary>
    public bool IsPermanentAt(DateTimeOffset moment) =>
        !IsVoluntary && EndedUtc > moment.AddYears(PermanentThresholdYears);
}
