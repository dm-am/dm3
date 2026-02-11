using System;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Users.LoginChange;

/// <summary>
/// Service DTO for a login change request
/// </summary>
public class LoginChangeRequestEntry
{
    /// <summary>
    /// Request identifier
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// Current login
    /// </summary>
    public string CurrentLogin { get; set; } = null!;

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Requested new login
    /// </summary>
    public string RequestedLogin { get; set; } = null!;

    /// <summary>
    /// Reason for the change
    /// </summary>
    public string Reason { get; set; } = null!;

    /// <summary>
    /// Request status
    /// </summary>
    public LoginChangeRequestStatus Status { get; set; }

    /// <summary>
    /// When the request was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the request was resolved
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// Who resolved the request (login)
    /// </summary>
    public string? ResolvedByLogin { get; set; }

    /// <summary>
    /// Resolver's comment
    /// </summary>
    public string? ResolverComment { get; set; }
}
