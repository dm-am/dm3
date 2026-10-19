namespace DM.Domain.Moderation.Features.Bans;

/// <summary>
/// What happened to a ban at a moment of the ban list
/// </summary>
public enum BanLogEntryKind
{
    /// <summary>
    /// The ban was issued, at its start
    /// </summary>
    Issued = 1,

    /// <summary>
    /// The ban was lifted early, at the moment of the lift
    /// </summary>
    Lifted = 2
}
