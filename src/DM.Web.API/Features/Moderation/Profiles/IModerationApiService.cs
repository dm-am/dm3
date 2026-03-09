using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// User account information for moderation purposes (DEVELOPMENT ONLY)
/// </summary>
public class TestAccountInfo
{
    /// <summary>
    /// User login (username)
    /// </summary>
    /// <example>admin</example>
    public string Login { get; set; } = "";

    /// <summary>
    /// Password field (deprecated - no longer populated for security)
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }

    /// <summary>
    /// Current user role
    /// </summary>
    /// <example>Admin</example>
    public UserRole Role { get; set; }
}

/// <summary>
/// Result of seeding test users
/// </summary>
public class SeedResult
{
    /// <summary>
    /// Number of users created
    /// </summary>
    public int Created { get; set; }

    /// <summary>
    /// Number of users skipped (already exist)
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// List of created user logins
    /// </summary>
    public List<string> CreatedLogins { get; set; } = new();

    /// <summary>
    /// List of skipped user logins (already exist)
    /// </summary>
    public List<string> SkippedLogins { get; set; } = new();
}

/// <summary>
/// API service for moderation and administration
/// </summary>
public interface IModerationApiService
{
    /// <summary>
    /// Set role for the current authenticated user
    /// </summary>
    /// <param name="role">New role to assign</param>
    Task SetRole(UserRole role);

    /// <summary>
    /// Get all registered users with their roles
    /// </summary>
    /// <returns>List of users sorted by role and login</returns>
    Task<IReadOnlyList<TestAccountInfo>> GetAllUsers();

    /// <summary>
    /// Moderate user profile (SeniorModerator+ only)
    /// </summary>
    /// <param name="login">User login</param>
    /// <param name="profile">Moderation data</param>
    /// <returns>Updated user profile</returns>
    Task<Envelope<UserProfile>> ModerateUserProfile(string login, ModerateProfile profile);

    /// <summary>
    /// Seed test users for development (DEVELOPMENT ONLY)
    /// </summary>
    /// <returns>Seed result with created/skipped counts</returns>
    Task<SeedResult> SeedTestUsers();
}
