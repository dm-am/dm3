using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Repository for dice rolls
/// </summary>
/// <remarks>
/// Read-only on purpose: the single point of writing is the creation of the
/// post, and the rolls travel inside <see cref="DM.Domain.Game.Features.Games.CreatePostEntity"/>
/// so that <see cref="IPostRepository.Create"/> writes the post and its rolls in
/// one transaction. Both or neither — the compensation this interface used to
/// carry existed only because there was no transaction across two stores.
/// </remarks>
public interface IDiceRollRepository
{
    /// <summary>
    /// Get dice rolls for a single post
    /// </summary>
    Task<IEnumerable<DiceRoll>> GetByPostIdAsync(Guid postId);

    /// <summary>
    /// Get dice rolls for multiple posts (batch fetch)
    /// </summary>
    Task<IDictionary<Guid, IEnumerable<DiceRoll>>> GetByPostIdsAsync(IEnumerable<Guid> postIds);
}
