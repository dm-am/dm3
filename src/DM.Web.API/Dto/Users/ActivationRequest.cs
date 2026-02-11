using System;
using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO for completing activation with login selection
/// </summary>
public class ActivationRequest
{
    /// <summary>
    /// Activation token from email link
    /// </summary>
    [Required]
    public Guid Token { get; set; }

    /// <summary>
    /// Chosen login (displayed username, 2-20 characters)
    /// </summary>
    /// <remarks>
    /// Forbidden characters: control chars, HTML unsafe (&lt;&gt;), quotes ("'`), backslash.
    /// Cannot start/end with spaces or have consecutive spaces.
    /// </remarks>
    /// <example>JohnDoe_123</example>
    [Required(ErrorMessage = "Login is required")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "Login must be between 2 and 20 characters")]
    public string Login { get; set; } = "";

    /// <summary>
    /// Optional: email from sessionStorage for idempotent retry detection.
    /// If activation already completed for this email+login, return success.
    /// </summary>
    public string? ExpectedEmail { get; set; }
}
