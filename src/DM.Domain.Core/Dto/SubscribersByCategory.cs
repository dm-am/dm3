namespace DM.Domain.Core.Dto;

/// <summary>
/// How many people subscribe to a user for each of the three things a user
/// produces.
/// </summary>
/// <remarks>
/// The names on the profile lines come from one preview capped at
/// <see cref="Configuration.SubscriptionPolicy.PreviewCap" /> before categories,
/// so only these counts say how many each line really has.
///
/// Three named fields rather than a map keyed by
/// <see cref="Enums.SubscriptionSettings" />: the three categories are a fixed
/// product concept — the profile has exactly three tabs — and a public response
/// reading <c>{ "games": 5 }</c> is worth more than one reading
/// <c>{ "512": 5 }</c>.
/// </remarks>
public class SubscribersByCategory
{
    /// <summary>
    /// Subscribers carrying <see cref="Enums.SubscriptionSettings.AuthorGameEvents" />.
    /// </summary>
    public int Games { get; set; }

    /// <summary>
    /// Subscribers carrying <see cref="Enums.SubscriptionSettings.AuthorBlogEvents" />.
    /// </summary>
    public int Blogs { get; set; }

    /// <summary>
    /// Subscribers carrying <see cref="Enums.SubscriptionSettings.AuthorTopicEvents" />.
    /// </summary>
    public int Topics { get; set; }
}
