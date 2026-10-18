namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// The notice the previous address gets once the account has moved to another one
/// </summary>
/// <remarks>
/// The second half of the pair. The first letter goes out when the change is
/// asked for, while a new password still calls it off; this one says the move
/// happened, for the reader who never opened the first. The new address is
/// printed whole, as in the first letter, so the owner can tell a mailbox of
/// their own from a stranger's.
/// </remarks>
/// <param name="PreviousEmail">Address the account had, the one this letter goes to</param>
/// <param name="NewEmail">Address it moved to</param>
/// <param name="SupportUrl">Address of the support page</param>
public record EmailChangeCompletedViewModel(
    string PreviousEmail,
    string NewEmail,
    string SupportUrl);
