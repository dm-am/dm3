using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// Request to initiate username change (requires moderator approval)
/// </summary>
/// <remarks>
/// The length of the reason is the domain's rule (CreateUsernameChangeRequestValidator).
/// </remarks>
public class UsernameChangeCreateRequest
{
    /// <summary>
    /// Why user wants to change username
    /// </summary>
    [Required]
    public string Reason { get; set; } = string.Empty;
}
