using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Web.API.Features.Game.Invitations;

/// <summary>
/// API service for game invitations
/// </summary>
public interface IGameInvitationApiService
{
    /// <summary>
    /// Get pending invitations for a game
    /// </summary>
    Task<IEnumerable<GameInvitation>> GetGameInvitations(Guid gameId);

    /// <summary>
    /// Create a player invitation
    /// </summary>
    Task<GameInvitation> InvitePlayer(Guid gameId, string username);

    /// <summary>
    /// Create a reader invitation
    /// </summary>
    Task<GameInvitation> InviteReader(Guid gameId, string username);

    /// <summary>
    /// Create an assistant invitation
    /// </summary>
    Task<GameInvitation> InviteAssistant(Guid gameId, string username);

    /// <summary>
    /// Cancel an invitation
    /// </summary>
    Task CancelInvitation(Guid tokenId);
}
