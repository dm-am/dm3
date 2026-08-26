using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// Second step of a login: the code answering the challenge.
/// </summary>
/// <remarks>
/// One field, and deliberately one: a code from the device and a recovery code
/// are told apart by their shape, so the caller does not have to declare which
/// one it is sending - and a client that got the flag wrong would turn a valid
/// value into a refusal.
///
/// The challenge itself is not here. It travels in a cookie the browser sends by
/// itself, which is also what keeps it out of a body a script can read.
/// </remarks>
public class TwoFactorLoginRequest
{
    /// <summary>
    /// Code from the authenticator app, or one of the recovery codes
    /// </summary>
    [Required(ErrorMessage = "Введите код")]
    [StringLength(64, MinimumLength = 1, ErrorMessage = "Код от 1 до 64 символов")]
    public string Code { get; set; } = "";
}
