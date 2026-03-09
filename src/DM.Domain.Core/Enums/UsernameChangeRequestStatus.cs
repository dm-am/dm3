namespace DM.Domain.Core.Enums;

/// <summary>
/// Status of a username change request
/// </summary>
public enum UsernameChangeRequestStatus
{
    /// <summary>
    /// Request is pending moderator review
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Request was approved, user can now choose new username
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Request was rejected by moderator
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// Username change completed (user chose new name after approval)
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Request expired (pending request auto-rejected, or approval token not used in time)
    /// </summary>
    Expired = 4
}
