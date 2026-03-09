using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Web.API.Features.Personal.Invitations;

/// <summary>
/// API service for user's invitations (combines game and blog invitations)
/// </summary>
public interface IPersonalInvitationApiService
{
    /// <summary>
    /// Get all pending invitations for current user (games and blogs)
    /// </summary>
    Task<IEnumerable<ReceivedInvitation>> GetMyInvitations();

    /// <summary>
    /// Accept an invitation
    /// </summary>
    Task AcceptInvitation(Guid tokenId);

    /// <summary>
    /// Reject an invitation
    /// </summary>
    Task RejectInvitation(Guid tokenId);
}
