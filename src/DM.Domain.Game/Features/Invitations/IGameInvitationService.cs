using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Invitations;

/// <summary>
/// Service for managing game members and invitations
/// </summary>
public interface IGameInvitationService
{
    #region Invitations

    /// <summary>
    /// Invite user as player (creates character application slot)
    /// </summary>
    Task<GameInvitation> InvitePlayer(Guid gameId, string username, CancellationToken ct = default);

    /// <summary>
    /// Invite user as reader (auto-subscribes on accept)
    /// </summary>
    Task<GameInvitation> InviteReader(Guid gameId, string username, CancellationToken ct = default);

    /// <summary>
    /// Invite user as assistant
    /// </summary>
    Task<GameInvitation> InviteAssistant(Guid gameId, string username, CancellationToken ct = default);

    /// <summary>
    /// Accept any invitation by token
    /// </summary>
    Task AcceptInvitation(Guid tokenId, CancellationToken ct = default);

    /// <summary>
    /// Reject any invitation by token
    /// </summary>
    Task RejectInvitation(Guid tokenId, CancellationToken ct = default);

    /// <summary>
    /// Cancel invitation (owner or assistant only)
    /// </summary>
    Task CancelInvitation(Guid tokenId, CancellationToken ct = default);

    #endregion

    #region Users

    /// <summary>
    /// Get all users with their roles
    /// </summary>
    Task<IEnumerable<GameUser>> GetUsers(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Get pending invitations for a game
    /// </summary>
    Task<IEnumerable<GameInvitation>> GetPendingInvitations(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Get user's pending invitations across all games
    /// </summary>
    Task<IEnumerable<GameInvitation>> GetUserInvitations(CancellationToken ct = default);

    /// <summary>
    /// Remove assistant from game (master only)
    /// </summary>
    Task RemoveUser(Guid gameId, Guid userId, CancellationToken ct = default);

    #endregion
}
