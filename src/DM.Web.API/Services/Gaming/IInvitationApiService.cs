using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Web.API.Dto.Games;

namespace DM.Web.API.Services.Gaming;

/// <summary>
/// API service for game invitations
/// </summary>
public interface IInvitationApiService
{
    /// <summary>
    /// Get pending invitations for a game
    /// </summary>
    Task<IEnumerable<Invitation>> GetGameInvitations(Guid gameId);

    /// <summary>
    /// Get pending invitations for current user
    /// </summary>
    Task<IEnumerable<Invitation>> GetUserInvitations();

    /// <summary>
    /// Create a player invitation
    /// </summary>
    Task<Invitation> InvitePlayer(Guid gameId, string login);

    /// <summary>
    /// Create a reader invitation
    /// </summary>
    Task<Invitation> InviteReader(Guid gameId, string login);

    /// <summary>
    /// Cancel an invitation
    /// </summary>
    Task CancelInvitation(Guid tokenId);

    /// <summary>
    /// Accept an invitation
    /// </summary>
    Task AcceptInvitation(Guid tokenId);

    /// <summary>
    /// Reject an invitation
    /// </summary>
    Task RejectInvitation(Guid tokenId);
}
