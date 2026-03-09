using System;
using System.Threading.Tasks;

namespace DM.Web.API.Features.Moderation.Mentorships;

/// <summary>
/// API service for mentorship operations
/// </summary>
public interface IMentorshipApiService
{
    /// <summary>
    /// Assign current user as mentor for a game
    /// </summary>
    Task AssignGameMentor(Guid gameId);

    /// <summary>
    /// Remove current user as mentor from a game
    /// </summary>
    Task RemoveGameMentor(Guid gameId);

    /// <summary>
    /// Assign current user as mentor for a blog
    /// </summary>
    Task AssignBlogMentor(Guid blogId);

    /// <summary>
    /// Remove current user as mentor from a blog
    /// </summary>
    Task RemoveBlogMentor(Guid blogId);
}
