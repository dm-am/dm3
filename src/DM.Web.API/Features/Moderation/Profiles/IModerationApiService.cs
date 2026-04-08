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
    /// User username
    /// </summary>
    /// <example>admin</example>
    public string Username { get; set; } = "";

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
    /// List of created user usernames
    /// </summary>
    public List<string> CreatedUsernames { get; set; } = new();

    /// <summary>
    /// List of skipped user usernames (already exist)
    /// </summary>
    public List<string> SkippedUsernames { get; set; } = new();
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

    /// <summary>
    /// Seed comprehensive test data including forums, games, blogs, chats, reviews (DEVELOPMENT ONLY)
    /// </summary>
    /// <returns>Seed result with details</returns>
    Task<ComprehensiveSeedResult> SeedComprehensiveData();
}

/// <summary>
/// Result of comprehensive data seeding
/// </summary>
public class ComprehensiveSeedResult
{
    /// <summary>
    /// Number of topics created
    /// </summary>
    public int TopicsCreated { get; set; }

    /// <summary>
    /// Number of comments created
    /// </summary>
    public int CommentsCreated { get; set; }

    /// <summary>
    /// Number of games created
    /// </summary>
    public int GamesCreated { get; set; }

    /// <summary>
    /// Number of characters created
    /// </summary>
    public int CharactersCreated { get; set; }

    /// <summary>
    /// Number of posts created
    /// </summary>
    public int PostsCreated { get; set; }

    /// <summary>
    /// Number of blogs created
    /// </summary>
    public int BlogsCreated { get; set; }

    /// <summary>
    /// Number of publications created
    /// </summary>
    public int PublicationsCreated { get; set; }

    /// <summary>
    /// Number of messages created
    /// </summary>
    public int MessagesCreated { get; set; }

    /// <summary>
    /// Number of reviews created (user, game, post reviews)
    /// </summary>
    public int ReviewsCreated { get; set; }

    /// <summary>
    /// Number of testimonials created (website reviews)
    /// </summary>
    public int TestimonialsCreated { get; set; }

    /// <summary>
    /// Number of polls created
    /// </summary>
    public int PollsCreated { get; set; }

    /// <summary>
    /// Number of likes created
    /// </summary>
    public int LikesCreated { get; set; }

    /// <summary>
    /// Number of board moderators assigned
    /// </summary>
    public int BoardModeratorsAssigned { get; set; }

    /// <summary>
    /// Skipped items (already existed)
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Details about what was created
    /// </summary>
    public List<string> Details { get; set; } = new();
}
