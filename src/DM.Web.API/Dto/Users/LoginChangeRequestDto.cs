using System;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// API DTO for a login change request
/// </summary>
public class LoginChangeRequestDto
{
    /// <summary>
    /// Request identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Current login
    /// </summary>
    public string CurrentLogin { get; set; } = null!;

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
    /// When the request was created (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the request was resolved (UTC)
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; set; }

    /// <summary>
    /// Who resolved the request (moderator login)
    /// </summary>
    public string? ResolvedByLogin { get; set; }

    /// <summary>
    /// Resolver's comment
    /// </summary>
    public string? ResolverComment { get; set; }
}
