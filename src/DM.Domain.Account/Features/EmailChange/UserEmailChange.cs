namespace DM.Domain.Account.Features.EmailChange;

/// <summary>
/// DTO for email change
/// </summary>
public class UserEmailChange
{
    /// <summary>
    /// User's display name (unique username)
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// User password
    /// </summary>
    public string Password { get; set; } = null!;

    /// <summary>
    /// New user email
    /// </summary>
    public string Email { get; set; } = null!;
}