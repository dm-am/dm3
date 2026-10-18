namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for username change approval letter
/// </summary>
/// <param name="ApprovalLinkUrl">Link to choose the new username</param>
public record UsernameChangeApprovalViewModel(
    string ApprovalLinkUrl);
