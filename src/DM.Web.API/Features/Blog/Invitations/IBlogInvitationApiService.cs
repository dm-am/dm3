using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace DM.Web.API.Features.Blog.Invitations;

/// <summary>
/// API service for blog invitations (owner operations)
/// </summary>
/// <remarks>
/// For accepting/rejecting invitations as recipient, use Personal API
/// </remarks>
public interface IBlogInvitationApiService
{
    /// <summary>
    /// Create assistant invitation for a blog
    /// </summary>
    Task<BlogInvitation> CreateAssistantInvitation(Guid blogId, string username);

    /// <summary>
    /// Create reader invitation for a blog
    /// </summary>
    Task<BlogInvitation> CreateReaderInvitation(Guid blogId, string username);

    /// <summary>
    /// Cancel invitation (by blog owner)
    /// </summary>
    Task CancelInvitation(Guid tokenId);

    /// <summary>
    /// Get pending invitations for a blog
    /// </summary>
    Task<IEnumerable<BlogInvitation>> GetBlogInvitations(Guid blogId);

    /// <summary>
    /// Get pending invitations for current user
    /// </summary>
    Task<IEnumerable<BlogInvitation>> GetMyInvitations();

    /// <summary>
    /// Accept a blog invitation
    /// </summary>
    Task AcceptInvitation(Guid tokenId);

    /// <summary>
    /// Reject a blog invitation
    /// </summary>
    Task RejectInvitation(Guid tokenId);
}
