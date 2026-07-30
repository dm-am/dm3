namespace DM.Domain.Core.Configuration;

/// <summary>
/// Product rules for the subscriber previews that games, blogs and profiles show.
/// </summary>
/// <remarks>
/// Held here so that a screen can never quietly show a different number of names
/// than its neighbour, and so the ordering rule below has one place to be stated.
/// </remarks>
public static class SubscriptionPolicy
{
    /// <summary>
    /// How many subscriber names a preview carries.
    /// </summary>
    /// <remarks>
    /// The full subscriber list is never sent: the previews feed tooltips and a
    /// one-line profile section, and the total is carried as a count beside them.
    /// The names selected are the most recently active subscribers, newest first,
    /// tie-broken by subscription id so the boundary of the cap is deterministic
    /// rather than whatever order the database happened to return. Subscribers
    /// who have never been active sort last.
    /// </remarks>
    public const int PreviewCap = 20;
}
