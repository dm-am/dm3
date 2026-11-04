using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// Where a notification points, for the three channels that reach a reader who is
/// not looking at the site.
/// </summary>
/// <remarks>
/// The path is NotificationDestination's, the table the list on the site reads too; this adds the root.
/// </remarks>
internal static class NotificationLink
{
    /// <summary>The words of the link in a letter and in a Telegram message.</summary>
    public const string Caption = "Перейти";

    /// <summary>
    /// Absolute address of what a notification is about, or null when there is
    /// nothing to point at.
    /// </summary>
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
