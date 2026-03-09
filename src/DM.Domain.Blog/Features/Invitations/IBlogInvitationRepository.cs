using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Invitations;

/// <summary>
/// Repository for blog invitations
/// </summary>
public interface IBlogInvitationRepository
{
    /// <summary>
    /// Find existing invitations for a user in a blog
    /// </summary>
    Task<IEnumerable<Guid>> FindInvitations(Guid blogId, Guid userId, TokenType type, CancellationToken ct = default);

    /// <summary>
    /// Invalidate old invitations and create a new one
    /// </summary>
    /// <param name="invitationsToInvalidate">Token IDs to invalidate</param>
    /// <param name="entity">New invitation to create</param>
    /// <param name="ct">Cancellation token</param>
    Task InvalidateAndCreate(IEnumerable<Guid> invitationsToInvalidate, CreateBlogInvitationEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Invalidate (soft delete) an invitation
    /// </summary>
    Task Invalidate(Guid tokenId, CancellationToken ct = default);

    /// <summary>
    /// Get an invitation by token ID
    /// </summary>
    Task<BlogInvitation?> GetInvitation(Guid tokenId, CancellationToken ct = default);

    /// <summary>
    /// Get pending invitations for a blog
    /// </summary>
    Task<IEnumerable<BlogInvitation>> GetPendingInvitations(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Get pending invitations for a user
    /// </summary>
    Task<IEnumerable<BlogInvitation>> GetUserPendingInvitations(Guid userId, CancellationToken ct = default);
}
