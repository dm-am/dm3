using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Posts;

/// <remarks>
/// Read-only: the rolls travel inside <see cref="CreatePostEntity"/>, so
/// <see cref="IPostRepository.Create"/> writes the post and its rolls in one transaction.
/// </remarks>
public interface IDiceRollRepository
{
    Task<IEnumerable<DiceRoll>> GetByPostIdAsync(Guid postId, CancellationToken ct = default);

    Task<IDictionary<Guid, IEnumerable<DiceRoll>>> GetByPostIdsAsync(IEnumerable<Guid> postIds);
}
