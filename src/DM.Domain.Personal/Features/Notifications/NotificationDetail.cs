namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// One field of a notification as the list in the application shows it.
/// </summary>
/// <param name="Label">What the field is called, in the words of the letter</param>
/// <param name="Value">The value as the generator wrote it</param>
/// <param name="IsMoment">
/// Whether the value is a point in time, which the reader's clock formats like
/// every date of the site
/// </param>
public sealed record NotificationDetail(string Label, string Value, bool IsMoment);
