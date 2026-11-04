using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Blacklists;

public interface IBlogBlacklistRepository
{
    Task<IEnumerable<GeneralUser>> GetBlacklist(Guid blogId, CancellationToken ct = default);

    /// <summary>
    /// Add user to blog blacklist
    /// </summary>
    /// <remarks>
    /// Throws <see cref="DM.Domain.Core.Exceptions.DuplicateEntityException" /> when the
    /// pair is already on the list: the caller checks first, and this is the race
    /// where two requests both pass that check. The unique index on the pair is
    /// what holds it in the meantime, as it does for the game's list.
    /// </remarks>
    Task Add(Guid blogId, Guid blockedUserId, Guid blockedByUserId, CancellationToken ct = default);

    Task Remove(Guid blogId, Guid userId, CancellationToken ct = default);

    /// <returns>Token ids of the cancelled invitations</returns>
    Task<IEnumerable<Guid>> CancelInvitationsForUser(Guid blogId, Guid userId, CancellationToken ct = default);

    /// <returns>Number of entries copied</returns>
    Task<int> CopyFromPersonalBlacklist(Guid blogId, Guid ownerId, CancellationToken ct = default);
}
