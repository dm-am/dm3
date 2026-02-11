namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Status of a login change request
/// </summary>
public enum LoginChangeRequestStatus
{
    /// <summary>
    /// Request is pending review
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Request was approved and login was changed
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Request was rejected
    /// </summary>
    Rejected = 2
}
