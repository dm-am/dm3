namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// The letter that says the account is gone
/// </summary>
/// <remarks>
/// Nothing here can be undone from the mailbox. Moderation removed the account
/// and moderation is the only way back, so the letter points at the support
/// form rather than at the sign-in one: a removed account has no session, and
/// signing in changes nothing.
/// </remarks>
/// <param name="SupportUrl">Address of the support page</param>
public record AccountRemovedViewModel(string SupportUrl);
