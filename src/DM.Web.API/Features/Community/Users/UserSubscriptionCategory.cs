namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// A category of a subscription to a user and what its notifications are about.
/// </summary>
public class UserSubscriptionCategory
{
    /// <summary>
    /// The bit of the category in the settings of a subscription (the <c>SubscriptionSettings</c>
    /// [Flags] enum), as a number, as <see cref="SubscriberRef.Settings"/> is.
    /// </summary>
    public int Category { get; set; }

    /// <summary>
    /// The events the dispatcher notifies of under the bit, in the words of the subscribe panel.
    /// </summary>
    public string Events { get; set; } = string.Empty;
}
