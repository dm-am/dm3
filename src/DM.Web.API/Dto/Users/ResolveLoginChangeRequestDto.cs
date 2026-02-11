using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// API DTO for resolving (approving/rejecting) a login change request
/// </summary>
public class ResolveLoginChangeRequestDto
{
    /// <summary>
    /// New status (Approved or Rejected)
    /// </summary>
    public LoginChangeRequestStatus Status { get; set; }

    /// <summary>
    /// Optional comment from moderator
    /// </summary>
    public string? Comment { get; set; }
}
