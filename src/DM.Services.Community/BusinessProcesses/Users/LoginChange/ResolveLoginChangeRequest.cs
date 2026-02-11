using System;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Users.LoginChange;

/// <summary>
/// DTO for resolving (approving/rejecting) a login change request
/// </summary>
public class ResolveLoginChangeRequest
{
    /// <summary>
    /// Request identifier
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// New status (Approved or Rejected)
    /// </summary>
    public LoginChangeRequestStatus Status { get; set; }

    /// <summary>
    /// Optional comment from moderator
    /// </summary>
    public string? Comment { get; set; }
}
