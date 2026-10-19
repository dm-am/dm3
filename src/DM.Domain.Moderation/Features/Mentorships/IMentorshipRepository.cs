using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.Mentorships;

public interface IMentorshipRepository
{
    Task<Guid?> GetGameMentorId(Guid gameId, CancellationToken ct = default);

    Task SetGameMentor(Guid gameId, Guid? mentorId, CancellationToken ct = default);

    Task<bool> GameExists(Guid gameId, CancellationToken ct = default);

    Task<Guid?> GetBlogMentorId(Guid blogId, CancellationToken ct = default);

    Task SetBlogMentor(Guid blogId, Guid? mentorId, CancellationToken ct = default);

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
