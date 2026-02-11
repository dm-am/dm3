using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Gaming.Dto.Output;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Reading;

/// <summary>
/// Service for reading votes
/// </summary>
public interface IVoteReadingService
{
    /// <summary>
    /// Get all votes for a post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <returns>Collection of votes</returns>
    Task<IEnumerable<Vote>> GetByPost(Guid postId);

    /// <summary>
    /// Get single vote by identifier
    /// </summary>
    /// <param name="voteId">Vote identifier</param>
    /// <returns>Vote</returns>
    Task<Vote> Get(Guid voteId);
}
