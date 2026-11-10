using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Blog.Features.Invitations;

public interface IBlogInvitationService
{
    Task<BlogInvitation> InviteAssistant(Guid blogId, string username);

    Task<BlogInvitation> InviteReader(Guid blogId, string username);

    Task AcceptInvitation(Guid tokenId);

    Task RejectInvitation(Guid tokenId);

    /// <summary>
    /// Cancel an invitation (by blog owner)
    /// </summary>
    Task CancelInvitation(Guid tokenId);

    Task<IEnumerable<BlogInvitation>> GetPendingInvitations(Guid blogId);

    Task<IEnumerable<BlogInvitation>> GetUserPendingInvitations();
}
