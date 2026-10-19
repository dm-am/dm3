namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for the letter a notification is sent as
/// </summary>
/// <param name="Title">What happened: the heading of the letter</param>
/// <param name="Facts">The names and values the notification carries, in the order the list shows them</param>
/// <param name="LinkUrl">Absolute address of what the notification is about, when it has one</param>
/// <param name="LinkCaption">Caption of the button that leads there</param>
public record NotificationLetterViewModel(
    string Title,
    IReadOnlyList<KeyValuePair<string, string>> Facts,
    string? LinkUrl,
    string LinkCaption);
