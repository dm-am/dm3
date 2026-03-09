using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.Mentorships;

/// <summary>
/// Service for managing premoderation mentorship for games and blogs
/// </summary>
public interface IMentorshipService
{
    /// <summary>
    /// Assign current user as mentor for a game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task AssignGameMentor(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Remove current user as mentor from a game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task RemoveGameMentor(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Assign current user as mentor for a blog
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task AssignBlogMentor(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Remove current user as mentor from a blog
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task RemoveBlogMentor(Guid blogId, CancellationToken ct = default);
}
