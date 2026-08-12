using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// Request to complete username change after approval
/// </summary>
public class UsernameChangeCompletionRequest
{
    /// <summary>
    /// Chosen new username (2-20 characters)
    /// </summary>
    /// <remarks>
    /// The characters are the domain validator's rule, for the same reason as in
    /// ActivationRequest: an allow-list here contradicted the deny-list behind
    /// it, and the two answered differently about the same name.
    /// </remarks>
    [Required(ErrorMessage = "Имя пользователя обязательно")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "От 2 до 20 символов")]
    public string Username { get; set; } = string.Empty;
}
