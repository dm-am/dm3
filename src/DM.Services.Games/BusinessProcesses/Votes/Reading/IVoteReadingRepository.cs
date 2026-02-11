using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Gaming.Dto.Output;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Reading;

/// <summary>
/// Repository for reading votes
/// </summary>
public interface IVoteReadingRepository
{
    /// <summary>
    /// Get votes for a post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <returns>Collection of votes</returns>
    Task<IEnumerable<Vote>> GetByPost(Guid postId);

    /// <summary>
    /// Get single vote by identifier
    /// </summary>
    /// <param name="voteId">Vote identifier</param>
    /// <returns>Vote or null if not found</returns>
    Task<Vote> Get(Guid voteId);

    /// <summary>
    /// Check if user has already voted on a post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns>True if user has voted</returns>
    Task<bool> HasVoted(Guid postId, Guid userId);
}
