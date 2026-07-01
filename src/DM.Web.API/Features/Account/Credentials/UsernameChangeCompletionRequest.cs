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
    [Required(ErrorMessage = "Имя пользователя обязательно")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "От 2 до 20 символов")]
    [RegularExpression(@"^(?!.*  )[a-zA-Zа-яА-ЯеЕ0-9]([a-zA-Zа-яА-ЯеЕ0-9_.\- ]*[a-zA-Zа-яА-ЯеЕ0-9])?$",
        ErrorMessage = "Недопустимые символы или формат")]
    public string Username { get; set; } = string.Empty;
}
