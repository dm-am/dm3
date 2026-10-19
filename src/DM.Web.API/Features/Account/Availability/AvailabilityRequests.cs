using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Availability;

/// <summary>
/// The address a registration form is asking about.
/// </summary>
/// <remarks>
/// A body and not a query string: nginx logs the query with the request line, and
/// every keystroke of the form would pile up there as a list of real addresses.
/// </remarks>
public class EmailAvailabilityRequest
{
    /// <summary>
    /// Email to check
    /// </summary>
    [Required]
    public string Email { get; set; } = null!;
}

/// <summary>
/// The username a registration or rename form is asking about.
/// </summary>
/// <remarks>
/// A body for the same reason as <see cref="EmailAvailabilityRequest" />.
/// </remarks>
public class UsernameAvailabilityRequest
{
    /// <summary>
    /// Username to check
    /// </summary>
    [Required]
    public string Username { get; set; } = null!;
}
