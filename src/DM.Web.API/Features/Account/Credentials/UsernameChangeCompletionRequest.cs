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
    /// <remarks>The characters are the domain validator's rule, as in ActivationRequest.</remarks>
    [Required(ErrorMessage = "Имя пользователя обязательно")]
    // Two at least: a name is the one address of a profile, and one-letter names would go to the first comers.
    [StringLength(20, MinimumLength = 2, ErrorMessage = "От 2 до 20 символов")]
    public string Username { get; set; } = string.Empty;
}
