using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Blog.Features.Invitations;

/// <summary>
/// Service for blog invitations
/// </summary>
public interface IBlogInvitationService
{
    /// <summary>
    /// Invite user as assistant
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="username">Username to invite</param>
    /// <returns>Created invitation</returns>
    Task<BlogInvitation> InviteAssistant(Guid blogId, string username);

    /// <summary>
    /// Invite user as reader
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="username">Username to invite</param>
    /// <returns>Created invitation</returns>
    Task<BlogInvitation> InviteReader(Guid blogId, string username);

    /// <summary>
    /// Accept any invitation by token
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    Task AcceptInvitation(Guid tokenId);

    /// <summary>
    /// Reject any invitation by token
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    Task RejectInvitation(Guid tokenId);

    /// <summary>
    /// Cancel an invitation (by blog owner)
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    Task CancelInvitation(Guid tokenId);

    /// <summary>
    /// Get pending invitations for a blog
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <returns>List of pending invitations</returns>
    Task<IEnumerable<BlogInvitation>> GetPendingInvitations(Guid blogId);

    /// <summary>
    /// Get pending invitations for the current user
    /// </summary>
    /// <returns>List of pending invitations</returns>
    Task<IEnumerable<BlogInvitation>> GetUserPendingInvitations();
}
