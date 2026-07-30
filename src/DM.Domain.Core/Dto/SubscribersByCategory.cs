namespace DM.Domain.Core.Dto;

/// <summary>
/// How many people subscribe to a user for each of the three things a user
/// produces.
/// </summary>
/// <remarks>
/// The profile shows one line per category and the names on those lines come out
/// of a single preview capped at
/// <see cref="Configuration.SubscriptionPolicy.PreviewCap" /> and ordered by
/// activity — the cap is applied before anything knows about categories. So a
/// line could hold every name it had and still be missing subscribers, or hold
/// none at all while the category had plenty, and it said neither. These counts
/// are what lets the line say how many there really are.
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
