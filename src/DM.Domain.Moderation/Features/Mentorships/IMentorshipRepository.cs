using System;
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
}
