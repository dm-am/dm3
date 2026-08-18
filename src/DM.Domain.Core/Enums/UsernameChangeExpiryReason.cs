namespace DM.Domain.Core.Enums;

/// <summary>
/// Which deadline turned a username change request into
/// <see cref="UsernameChangeRequestStatus.Expired"/>. Both roads end in the same
/// status but mean opposite things to the requester, and the status alone cannot
/// tell them apart.
/// </summary>
public enum UsernameChangeExpiryReason
{
    /// <summary>
    /// No moderator opened the request before the review window closed. Nothing
    /// was ever granted or denied.
    /// </summary>
    Unreviewed = 0,

    /// <summary>
    /// The request was approved, but the approval link was never used and its
    /// window closed. The grant existed and lapsed unspent.
    /// </summary>
    ApprovalLapsed = 1
}
