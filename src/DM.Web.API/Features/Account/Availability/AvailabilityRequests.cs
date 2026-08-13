using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.Availability;

/// <summary>
/// The address a registration form is asking about.
/// </summary>
/// <remarks>
/// A body and not a query string. The check is a read, but the value being read
/// about is the thing that must not be written down: nginx logs the request line
/// with its query, the log store keeps a month of lines, and every registration form
/// keystroke would have accumulated there as a list of real addresses. The
/// application's own logging already records only the reason and never the
/// identifier — this closes the half it does not control.
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
