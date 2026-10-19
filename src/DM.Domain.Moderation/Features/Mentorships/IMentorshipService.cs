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
    /// Assign the current user as mentor for a game
    /// </summary>
    Task AssignGameMentor(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Remove the current user as mentor from a game
    /// </summary>
    Task RemoveGameMentor(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Assign the current user as mentor for a blog
    /// </summary>
    Task AssignBlogMentor(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Remove the current user as mentor from a blog
    /// </summary>
    Task RemoveBlogMentor(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get games curated by the given mentors (moderation overview, Moderator+)
    /// </summary>
    Task<IReadOnlyCollection<MentorshipAssignment>> GetGameMentorships(
        IReadOnlyCollection<Guid> mentorIds, CancellationToken ct = default);

    /// <summary>
    /// Get blogs curated by the given mentors (moderation overview, Moderator+)
    /// </summary>
    Task<IReadOnlyCollection<MentorshipAssignment>> GetBlogMentorships(
        IReadOnlyCollection<Guid> mentorIds, CancellationToken ct = default);
}
