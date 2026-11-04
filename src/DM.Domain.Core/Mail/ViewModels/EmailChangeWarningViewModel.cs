namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// The warning that goes to the current address when somebody asks to move the
/// account to another one
/// </summary>
/// <remarks>
/// The new address is printed whole: the owner tells a second mailbox of their own
/// from a stranger's by it.
/// </remarks>
/// <param name="CurrentEmail">Address the account has now, the one this letter goes to</param>
/// <param name="NewEmail">Address the request names</param>
public record EmailChangeWarningViewModel(
    string CurrentEmail,
    string NewEmail);
