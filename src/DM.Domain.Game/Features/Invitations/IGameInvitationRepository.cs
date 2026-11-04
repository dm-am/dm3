using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Invitations;

public interface IGameInvitationRepository
{
    Task<IEnumerable<GameUser>> GetUsers(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Writes the assistant and spends the invitation that made them one, in one commit
    /// </summary>
    /// <returns>True when this call spent the invitation; a spent link grants nothing.</returns>
    Task<bool> AcceptAssistantInvitation(AddAssistantEntity entity, Guid tokenId, CancellationToken ct = default);

    Task RemoveAssistant(Guid gameId, Guid userId, CancellationToken ct = default);

    Task<IEnumerable<GameInvitation>> GetPendingInvitations(Guid gameId, CancellationToken ct = default);

    Task<IEnumerable<GameInvitation>> GetUserInvitations(Guid userId, CancellationToken ct = default);

    Task<(GameInvitationToken? Token, GameInvitation? Info)> GetInvitation(Guid tokenId, CancellationToken ct = default);

    Task<GameInvitationToken> InvalidateAndCreateInvitation(CreateGameInvitationEntity entity, CancellationToken ct = default);

    /// <returns>True when this call spent the invitation, false when it was already spent.</returns>
    Task<bool> RemoveInvitation(Guid tokenId, CancellationToken ct = default);

    Task<IEnumerable<CancelledInvitation>> CancelInvitationsForUser(Guid gameId, Guid userId, CancellationToken ct = default);
}
