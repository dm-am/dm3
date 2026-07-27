using System;
using System.Collections.Generic;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Rolls dice server-side for a new post (doc 4.2.2.13). Turns the requested
/// roll specs into fully-populated <see cref="DiceRoll"/> models with results.
/// </summary>
public interface IDiceRoller
{
    /// <summary>
    /// Generate the results for the requested rolls of a single post.
    /// </summary>
    /// <param name="postId">Owning post identifier</param>
    /// <param name="createdUtc">Roll creation moment (matches the post)</param>
    /// <param name="specs">Requested roll specs</param>
    /// <returns>Rolled dice with computed results</returns>
    IReadOnlyList<DiceRoll> Roll(Guid postId, DateTimeOffset createdUtc, IEnumerable<CreatePostDiceRoll> specs);
}
