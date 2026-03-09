using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.Polls;

/// <summary>
/// Storage for polls
/// </summary>
public interface IPollRepository
{
    // ═══ READ ═══

    /// <summary>
    /// Count polls
    /// </summary>
    /// <param name="activeAt">Filter by end date (only active polls)</param>
    /// <returns></returns>
    Task<long> Count(DateTimeOffset? activeAt);

    /// <summary>
    /// Get list of polls
    /// </summary>
    /// <param name="activeAt">Filter by end date (only active polls)</param>
    /// <param name="pagingData">Paging data</param>
    /// <returns></returns>
    Task<IEnumerable<Poll>> Get(DateTimeOffset? activeAt, PagingData pagingData);

    /// <summary>
    /// Get single poll by id
    /// </summary>
    /// <param name="id">Poll identifier</param>
    /// <returns></returns>
    Task<Poll> Get(Guid id);

    // ═══ WRITE ═══

    /// <summary>
    /// Create new poll
    /// </summary>
    /// <param name="poll">Poll data</param>
    /// <returns></returns>
    Task<Poll> Create(CreatePollEntity poll);

    /// <summary>
    /// Update poll
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <param name="title">New title (null to keep current)</param>
    /// <param name="endDate">New end date (null to keep current)</param>
    /// <returns></returns>
    Task<Poll> Update(Guid pollId, string? title, DateTimeOffset? endDate);

    /// <summary>
    /// Mark poll as removed
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    Task Delete(Guid pollId);

    // ═══ VOTING ═══

    /// <summary>
    /// Vote for the poll option
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <param name="optionId">Option identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<Poll> Vote(Guid pollId, Guid optionId, Guid userId);

    /// <summary>
    /// Remove vote from the poll
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task<Poll> Unvote(Guid pollId, Guid userId);
}
