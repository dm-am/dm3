using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Invitations;

/// <summary>
/// Repository for game invitation operations
/// </summary>
public interface IGameInvitationRepository
{
    #region Users

    /// <summary>
    /// Get all users in a game
    /// </summary>
    Task<IEnumerable<GameUser>> GetUsers(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Writes the assistant and spends the invitation that made them one, in one commit
    /// </summary>
    Task AcceptAssistantInvitation(AddAssistantEntity entity, Guid tokenId, CancellationToken ct = default);

    /// <summary>
    /// Remove assistant from game
    /// </summary>
    Task RemoveAssistant(Guid gameId, Guid userId, CancellationToken ct = default);

    #endregion

    #region Invitations

    /// <summary>
    /// Get pending invitations for a game
    /// </summary>
    Task<IEnumerable<GameInvitation>> GetPendingInvitations(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Get user's pending invitations
    /// </summary>
    Task<IEnumerable<GameInvitation>> GetUserInvitations(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get invitation by token ID
    /// </summary>
    Task<(GameInvitationToken? Token, GameInvitation? Info)> GetInvitation(Guid tokenId, CancellationToken ct = default);

    /// <summary>
    /// Create invitation
    /// </summary>
    Task<GameInvitationToken> CreateInvitation(CreateGameInvitationEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Invalidate existing and create new invitation
    /// </summary>
    Task<GameInvitationToken> InvalidateAndCreateInvitation(CreateGameInvitationEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Remove invitation
    /// </summary>
    Task RemoveInvitation(Guid tokenId, CancellationToken ct = default);

    /// <summary>
    /// Cancel all invitations for user in game
    /// </summary>
    Task<IEnumerable<CancelledInvitation>> CancelInvitationsForUser(Guid gameId, Guid userId, CancellationToken ct = default);

    #endregion
}
