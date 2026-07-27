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
}
