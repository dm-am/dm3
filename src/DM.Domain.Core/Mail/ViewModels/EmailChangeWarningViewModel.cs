namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// The warning that goes to the current address when somebody asks to move the
/// account to another one
/// </summary>
/// <remarks>
/// The new address is printed whole. Masked, it hid the one thing the owner
/// needs it for, telling a second mailbox of their own from a stranger's, and
/// what it protected was the address of whoever filed the request.
/// </remarks>
/// <param name="CurrentEmail">Address the account has now, the one this letter goes to</param>
/// <param name="NewEmail">Address the request names</param>
public record EmailChangeWarningViewModel(
    string CurrentEmail,
    string NewEmail);
