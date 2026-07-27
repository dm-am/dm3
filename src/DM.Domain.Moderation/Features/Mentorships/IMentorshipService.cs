using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Get games curated by the given mentors (moderation overview, Moderator+)
    /// </summary>
    /// <param name="mentorIds">Mentor user ids</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Game mentorship assignments</returns>
    Task<IReadOnlyCollection<MentorshipAssignment>> GetGameMentorships(
        IReadOnlyCollection<Guid> mentorIds, CancellationToken ct = default);

    /// <summary>
    /// Get blogs curated by the given mentors (moderation overview, Moderator+)
    /// </summary>
    /// <param name="mentorIds">Mentor user ids</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Blog mentorship assignments</returns>
    Task<IReadOnlyCollection<MentorshipAssignment>> GetBlogMentorships(
        IReadOnlyCollection<Guid> mentorIds, CancellationToken ct = default);
}
