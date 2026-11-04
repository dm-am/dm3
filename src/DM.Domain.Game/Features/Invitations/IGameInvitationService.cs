using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Invitations;

public interface IGameInvitationService
{
    Task<GameInvitation> InvitePlayer(Guid gameId, string username, CancellationToken ct = default);

    /// <summary>
    /// Subscribes on accept
    /// </summary>
    Task<GameInvitation> InviteReader(Guid gameId, string username, CancellationToken ct = default);

    Task<GameInvitation> InviteAssistant(Guid gameId, string username, CancellationToken ct = default);

    Task AcceptInvitation(Guid tokenId, CancellationToken ct = default);

    Task RejectInvitation(Guid tokenId, CancellationToken ct = default);

    Task CancelInvitation(Guid tokenId, CancellationToken ct = default);

    Task<IEnumerable<GameUser>> GetUsers(Guid gameId, CancellationToken ct = default);

    Task<IEnumerable<GameInvitation>> GetPendingInvitations(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// The current user's pending invitations across all games
    /// </summary>
    Task<IEnumerable<GameInvitation>> GetUserInvitations(CancellationToken ct = default);

    /// <summary>
    /// Removes an assistant
    /// </summary>
    Task RemoveUser(Guid gameId, Guid userId, CancellationToken ct = default);
}
