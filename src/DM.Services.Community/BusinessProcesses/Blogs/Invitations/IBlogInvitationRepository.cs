using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;

namespace DM.Services.Community.BusinessProcesses.Blogs.Invitations;

/// <summary>
/// Repository for blog invitations
/// </summary>
internal interface IBlogInvitationRepository
{
    /// <summary>
    /// Find existing invitations for a user in a blog
    /// </summary>
    Task<IEnumerable<Guid>> FindInvitations(Guid blogId, Guid userId, TokenType type);

    /// <summary>
    /// Invalidate old invitations and create a new one
    /// </summary>
    Task InvalidateAndCreate(IEnumerable<IUpdateBuilder<Token>> updates, Token token);

    /// <summary>
    /// Update a token
    /// </summary>
    Task Update(IUpdateBuilder<Token> update);

    /// <summary>
    /// Find blog by invitation token
    /// </summary>
    Task<Guid?> FindBlogByToken(Guid tokenId, Guid userId, TokenType type);

    /// <summary>
    /// Get an invitation by token ID
    /// </summary>
    Task<BlogInvitationInfo?> GetInvitation(Guid tokenId);

    /// <summary>
    /// Get pending invitations for a blog
    /// </summary>
    Task<IEnumerable<BlogInvitationInfo>> GetPendingInvitations(Guid blogId);

    /// <summary>
    /// Get pending invitations for a user
    /// </summary>
    Task<IEnumerable<BlogInvitationInfo>> GetUserPendingInvitations(Guid userId);
}
