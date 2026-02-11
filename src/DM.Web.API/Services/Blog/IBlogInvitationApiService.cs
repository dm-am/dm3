using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Web.API.Dto.Blogs;
using DM.Web.API.Dto.Contracts;

namespace DM.Web.API.Services.Blog;

/// <summary>
/// API service for blog invitations
/// </summary>
public interface IBlogInvitationApiService
{
    /// <summary>
    /// Create assistant invitation
    /// </summary>
    Task<Envelope<BlogInvitation>> CreateAssistantInvitation(Guid blogId, string login);

    /// <summary>
    /// Create reader invitation
    /// </summary>
    Task<Envelope<BlogInvitation>> CreateReaderInvitation(Guid blogId, string login);

    /// <summary>
    /// Accept invitation
    /// </summary>
    Task AcceptInvitation(Guid tokenId);

    /// <summary>
    /// Reject invitation
    /// </summary>
    Task RejectInvitation(Guid tokenId);

    /// <summary>
    /// Cancel invitation (by blog owner)
    /// </summary>
    Task CancelInvitation(Guid tokenId);

    /// <summary>
    /// Get pending invitations for a blog
    /// </summary>
    Task<ListEnvelope<BlogInvitation>> GetBlogInvitations(Guid blogId);

    /// <summary>
    /// Get pending invitations for current user
    /// </summary>
    Task<ListEnvelope<BlogInvitation>> GetMyInvitations();
}
