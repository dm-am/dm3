using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Repository for dice rolls
/// </summary>
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

    /// <summary>
    /// Persist the given rolls (rolled server-side at post creation)
    /// </summary>
    Task CreateAsync(IEnumerable<DiceRoll> rolls);

    /// <summary>
    /// Drop the rolls of a post whose insert did not go through
    /// </summary>
    /// <remarks>
    /// The rolls are written before the post, for the reason DATA_STORAGE.md
    /// gives: there is no transaction across the two stores, and a roll is the
    /// one thing here that cannot be produced a second time — rolling again
    /// answers a different number. Written first they only ever outlive a post
    /// nobody saw; this is what removes them when that happens.
    /// </remarks>
    /// <param name="postId">Post the rolls were made for</param>
    Task DeleteByPostIdAsync(Guid postId);
}
