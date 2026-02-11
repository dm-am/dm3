using System.ComponentModel.DataAnnotations;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// User profile and display settings
/// </summary>
/// <remarks>
/// These settings control how the user's profile appears to others
/// and their personal display preferences.
/// </remarks>
public class UserSettings
{
    /// <summary>
    /// Whether the user's birthday is visible to other users
    /// </summary>
    public bool IsBirthdayVisible { get; set; }

    /// <summary>
    /// Whether the user's birth year is visible (only applies when birthday is visible)
    /// </summary>
    public bool IsBirthdayYearVisible { get; set; }

    /// <summary>
    /// Website color scheme preference
    /// </summary>
    public ColorSchema ColorSchema { get; set; }

    /// <summary>
    /// Greeting message sent to new mentees when assigned to this mentor
    /// </summary>
    /// <remarks>
    /// Only applicable for users with mentor privileges.
    /// Maximum 1000 characters.
    /// </remarks>
    [StringLength(1000, ErrorMessage = "Mentor greeting message must not exceed 1000 characters")]
    public string? MentorGreetingsMessage { get; set; }

    /// <summary>
    /// Paging limits for various list views
    /// </summary>
    public PagingLimits PagingLimits { get; set; } = new();
}
