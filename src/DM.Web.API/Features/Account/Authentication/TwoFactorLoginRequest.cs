using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// Second step of a login: the code answering the challenge.
/// </summary>
/// <remarks>
/// One field: a device code and a recovery code are told apart by their shape.
/// The challenge travels in a cookie.
/// </remarks>
public class TwoFactorLoginRequest
{
    /// <summary>
    /// Code from the authenticator app, or one of the recovery codes
    /// </summary>
    [Required(ErrorMessage = "Введите код")]
    [StringLength(64, ErrorMessage = "Код от 1 до 64 символов")]
    public string Code { get; set; } = "";
}
