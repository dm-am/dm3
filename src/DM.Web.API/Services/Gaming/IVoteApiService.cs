using System;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;

namespace DM.Web.API.Services.Gaming;

/// <summary>
/// API service for vote resources
/// </summary>
public interface IVoteApiService
{
    /// <summary>
    /// Get votes for a post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <returns>List envelope with votes</returns>
    Task<ListEnvelope<Vote>> GetByPost(Guid postId);

    /// <summary>
    /// Get single vote
    /// </summary>
    /// <param name="voteId">Vote identifier</param>
    /// <returns>Envelope with vote</returns>
    Task<Envelope<Vote>> Get(Guid voteId);

    /// <summary>
    /// Create new vote on a post
    /// </summary>
    /// <param name="postId">Post identifier</param>
    /// <param name="vote">Vote data</param>
    /// <returns>Envelope with created vote</returns>
    Task<Envelope<Vote>> Create(Guid postId, Vote vote);

    /// <summary>
    /// Delete existing vote
    /// </summary>
    /// <param name="voteId">Vote identifier</param>
    /// <returns>Task</returns>
    Task Delete(Guid voteId);
}
