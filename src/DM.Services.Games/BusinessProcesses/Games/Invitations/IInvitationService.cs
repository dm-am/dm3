using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Gaming.BusinessProcesses.Games.Invitations;

/// <summary>
/// Service for managing game invitations
/// </summary>
public interface IInvitationService
{
    /// <summary>
    /// Create a player invitation
    /// </summary>
    Task<Token> CreatePlayerInvitation(Guid gameId, Guid userId);

    /// <summary>
    /// Create a reader invitation
    /// </summary>
    Task<Token> CreateReaderInvitation(Guid gameId, Guid userId);

    /// <summary>
    /// Accept a player invitation
    /// </summary>
    Task AcceptPlayerInvitation(Guid tokenId);

    /// <summary>
    /// Reject a player invitation
    /// </summary>
    Task RejectPlayerInvitation(Guid tokenId);

    /// <summary>
    /// Accept a reader invitation
    /// </summary>
    Task AcceptReaderInvitation(Guid tokenId);

    /// <summary>
    /// Reject a reader invitation
    /// </summary>
    Task RejectReaderInvitation(Guid tokenId);

    /// <summary>
    /// Cancel an invitation (by game master)
    /// </summary>
    Task CancelInvitation(Guid tokenId);

    /// <summary>
    /// Get all pending invitations for a game
    /// </summary>
    Task<IEnumerable<InvitationInfo>> GetPendingInvitations(Guid gameId);

    /// <summary>
    /// Get all pending invitations for the current user
    /// </summary>
    Task<IEnumerable<InvitationInfo>> GetUserPendingInvitations();
}
