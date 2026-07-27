using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.Mentorships;

/// <summary>
/// Repository for mentorship operations
/// </summary>
public interface IMentorshipRepository
{
    /// <summary>
    /// Get current mentor ID for a game
    /// </summary>
    Task<Guid?> GetGameMentorId(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Set mentor for a game
    /// </summary>
    Task SetGameMentor(Guid gameId, Guid? mentorId, CancellationToken ct = default);

    /// <summary>
    /// Check if game exists
    /// </summary>
    Task<bool> GameExists(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Get current mentor ID for a blog
    /// </summary>
    Task<Guid?> GetBlogMentorId(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Set mentor for a blog
    /// </summary>
    Task SetBlogMentor(Guid blogId, Guid? mentorId, CancellationToken ct = default);

    /// <summary>
    /// Check if blog exists
    /// </summary>
    Task<bool> BlogExists(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get non-removed games curated by the given mentors
    /// </summary>
    Task<IReadOnlyCollection<MentorshipAssignment>> GetGameMentorships(
        IReadOnlyCollection<Guid> mentorIds, CancellationToken ct = default);

    /// <summary>
    /// Get non-removed blogs curated by the given mentors
    /// </summary>
    Task<IReadOnlyCollection<MentorshipAssignment>> GetBlogMentorships(
        IReadOnlyCollection<Guid> mentorIds, CancellationToken ct = default);
}
