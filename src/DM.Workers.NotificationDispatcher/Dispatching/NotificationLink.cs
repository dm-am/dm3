using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// Where a notification points, for the three channels that reach a reader who is
/// not looking at the site.
/// </summary>
/// <remarks>
/// The path is NotificationDestination's in the domain, the one table the list in
/// the application is given its path from as well, so a letter, Telegram, Discord
/// and the list lead one reader to one place. What this adds is the root: a letter
/// and a bot message resolve a path against nothing.
///
/// An event with no destination and a payload with no identifier both yield no link
/// rather than a link to the front page. A changed password is not a page, and a
/// link that lands on the front page is a promise the letter does not keep.
/// </remarks>
internal static class NotificationLink
{
    /// <summary>
    /// Absolute address of what a notification is about, or null when there is
    /// nothing to point at.
    /// </summary>
    /// <remarks>
    /// The root is the address this deployment answers on, which is configuration
    /// rather than a constant: a link built on one address and read by somebody who
    /// reaches only the other one leads nowhere.
    /// </remarks>
    /// <param name="eventType">Event the notification is about</param>
    /// <param name="metadata">Metadata bag of the notification</param>
    /// <param name="addresses">Addresses of the site, for the root of the link</param>
    public static string? GetUrl(
        EventType eventType, object? metadata, SiteAddressConfiguration addresses)
    {
        if (string.IsNullOrWhiteSpace(addresses.PublicUrl))
        {
            return null;
        }

        var path = NotificationDestination.GetPath(eventType, metadata);
        return path == null ? null : $"{addresses.PublicUrl.TrimEnd('/')}{path}";
    }
}
