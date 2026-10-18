namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for the letter that says the second factor is about to be taken
/// off, and how to stop it
/// </summary>
/// <paramref name="Due">
/// When the factor comes off, already written in the reader's zone
/// </paramref>
/// <paramref name="CancellationLinkUrl">
/// Link that calls the removal off
/// </paramref>
/// <remarks>
/// A written line and not a moment: which zone a reader keeps is the account's
/// business, and a template that converted one would be a second place deciding
/// it. The sender asks <c>ReaderTime</c> and hands the answer over.
/// </remarks>
public record TwoFactorRemovalScheduledViewModel(
    string Due,
    string CancellationLinkUrl);
