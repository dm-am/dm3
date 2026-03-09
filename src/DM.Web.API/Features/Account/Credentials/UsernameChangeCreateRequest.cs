using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// Request to initiate username change (requires moderator approval)
/// </summary>
public class UsernameChangeCreateRequest
{
    /// <summary>
    /// Why user wants to change username
    /// </summary>
    [Required]
    [MinLength(10, ErrorMessage = "Причина должна быть не менее 10 символов")]
    [MaxLength(500, ErrorMessage = "Причина должна быть не более 500 символов")]
    public string Reason { get; set; } = string.Empty;
}
