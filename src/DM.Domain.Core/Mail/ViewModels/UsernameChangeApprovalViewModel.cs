namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for username change approval letter
/// </summary>
/// <param name="ApprovalLinkUrl">Link to choose the new username</param>
/// <param name="LinkLifetimeHours">How long the link lives, as the server holds it</param>
public record UsernameChangeApprovalViewModel(
    string ApprovalLinkUrl,
    int LinkLifetimeHours);
