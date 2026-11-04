using System;

namespace DM.Domain.Moderation.Features.Bans;

/// <summary>
/// One entry of the ban list: an issue or a lift, at the moment it happened
/// </summary>
/// <remarks>
/// A ban is one row, issued and lifted in place, so a lifted ban gives two entries:
/// its issue at its start and its lift at the moment of the lift, which can stand
/// above the issues of later bans.
/// </remarks>
public class BanLogEntry
{
    /// <summary>
    /// The number of the entry in the whole list, counted from the oldest entry,
    /// which is 1. A new entry happens after every entry there is, so the number
    /// of an entry never changes, and it is the same in the list of one person.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Whether the entry is the issue or the lift
    /// </summary>
    public BanLogEntryKind Kind { get; set; }

    /// <summary>
    /// When it happened: the start for an issue, the lift for a lift
    /// </summary>
    public DateTimeOffset OccurredUtc { get; set; }

    /// <summary>
    /// The ban as it stands now, the same for both entries of a lifted one
    /// </summary>
    public Ban Ban { get; set; } = null!;
}
