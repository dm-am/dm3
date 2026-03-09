using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// API service for community users (public profiles)
/// </summary>
public interface ICommunityUserApiService
{
    /// <summary>
    /// Get community users with paging
    /// </summary>
    Task<ListEnvelope<User>> GetUsers(UsersQuery query);

    /// <summary>
    /// Get community users by role
    /// </summary>
    Task<ListEnvelope<User>> GetUsersByRole(UserRole role);

    /// <summary>
    /// Get community user by username
    /// </summary>
    Task<Envelope<User>> GetUser(string username);

    /// <summary>
    /// Get community user profile (detailed view)
    /// </summary>
    Task<Envelope<UserProfile>> GetUserProfile(string username);

    /// <summary>
    /// Get user's featured post
    /// </summary>
    Task<Envelope<FeaturedPost>> GetFeaturedPost(string username);

    /// <summary>
    /// Get login history by username
    /// </summary>
    Task<ListEnvelope<LoginHistoryDto>> GetLoginHistory(string username);
}
