using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// DTO for resolving (approving/rejecting) a username change request
/// </summary>
public class ResolveUsernameChangeRequest
{
    /// <summary>
    /// Request identifier
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// New status (Approved or Rejected)
    /// </summary>
    public UsernameChangeRequestStatus Status { get; set; }

    /// <summary>
    /// Optional comment from moderator
    /// </summary>
    public string? Comment { get; set; }
}
