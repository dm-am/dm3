using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.Polls;

/// <summary>
/// Service for poll operations
/// </summary>
public interface IPollService
{
    /// <summary>
    /// Create new poll and start immediately
    /// </summary>
    /// <param name="createPoll">Poll creation data</param>
    /// <returns>Created poll</returns>
    Task<Poll> CreateAsync(CreatePoll createPoll);

    /// <summary>
    /// Get single poll by identifier
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <returns>Poll</returns>
    Task<Poll> GetAsync(Guid pollId);

    /// <summary>
    /// Get list of polls
    /// </summary>
    /// <param name="query">Polls query with filters and sorting</param>
    /// <returns>Polls with paging info</returns>
    Task<(IEnumerable<Poll> Polls, PagingResult Paging)> GetListAsync(PollsQuery query);

    /// <summary>
    /// Update existing poll
    /// </summary>
    /// <param name="updatePoll">Poll update data</param>
    /// <returns>Updated poll</returns>
    Task<Poll> UpdateAsync(UpdatePoll updatePoll);

    /// <summary>
    /// Delete poll (soft delete)
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    Task DeleteAsync(Guid pollId);

    /// <summary>
    /// Vote for a certain poll option
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <param name="optionId">Option identifier</param>
    /// <returns>Updated poll</returns>
    Task<Poll> VoteAsync(Guid pollId, Guid optionId);

    /// <summary>
    /// Remove vote from the poll
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <returns>Updated poll</returns>
    Task<Poll> UnvoteAsync(Guid pollId);
}
