using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Request to create an invitation
/// </summary>
public class CreateInvitationRequest
{
    /// <summary>
    /// Username of user to invite
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;
}
