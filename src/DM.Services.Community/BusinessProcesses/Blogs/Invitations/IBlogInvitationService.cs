using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Blogs.Invitations;

/// <summary>
/// Service for blog invitations
/// </summary>
public interface IBlogInvitationService
{
    /// <summary>
    /// Create an assistant invitation
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns>Created token</returns>
    Task<Token> CreateAssistantInvitation(Guid blogId, Guid userId);

    /// <summary>
    /// Create a reader invitation
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns>Created token</returns>
    Task<Token> CreateReaderInvitation(Guid blogId, Guid userId);

    /// <summary>
    /// Accept an assistant invitation
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    Task AcceptAssistantInvitation(Guid tokenId);

    /// <summary>
    /// Reject an assistant invitation
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    Task RejectAssistantInvitation(Guid tokenId);

    /// <summary>
    /// Accept a reader invitation
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    Task AcceptReaderInvitation(Guid tokenId);

    /// <summary>
    /// Reject a reader invitation
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    Task RejectReaderInvitation(Guid tokenId);

    /// <summary>
    /// Cancel an invitation
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    Task CancelInvitation(Guid tokenId);

    /// <summary>
    /// Get pending invitations for a blog
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <returns>List of pending invitations</returns>
    Task<IEnumerable<BlogInvitationInfo>> GetPendingInvitations(Guid blogId);

    /// <summary>
    /// Get pending invitations for the current user
    /// </summary>
    /// <returns>List of pending invitations</returns>
    Task<IEnumerable<BlogInvitationInfo>> GetUserPendingInvitations();
}

/// <summary>
/// Information about a blog invitation
/// </summary>
public class BlogInvitationInfo
{
    /// <summary>
    /// Token identifier
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Blog title
    /// </summary>
    public string BlogTitle { get; set; } = "";

    /// <summary>
    /// Invited user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Invited user login
    /// </summary>
    public string UserLogin { get; set; } = "";

    /// <summary>
    /// Invitation type (Assistant or Reader)
    /// </summary>
    public TokenType Type { get; set; }

    /// <summary>
    /// When the invitation was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
