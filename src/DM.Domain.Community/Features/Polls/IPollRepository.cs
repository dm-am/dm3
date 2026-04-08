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
    /// Count polls matching query
    /// </summary>
    /// <param name="query">Query parameters</param>
    /// <returns>Number of polls</returns>
    Task<long> Count(PollsQuery query);

    /// <summary>
    /// Get list of polls
    /// </summary>
    /// <param name="query">Query parameters</param>
    /// <param name="pagingData">Paging data</param>
    /// <returns>List of polls</returns>
    Task<IEnumerable<Poll>> Get(PollsQuery query, PagingData pagingData);

    /// <summary>
    /// Get single poll by id
    /// </summary>
    /// <param name="id">Poll identifier</param>
    /// <returns>Poll</returns>
    Task<Poll> Get(Guid id);

    // ═══ WRITE ═══

    /// <summary>
    /// Create new poll
    /// </summary>
    /// <param name="poll">Poll data</param>
    /// <returns>Created poll</returns>
    Task<Poll> Create(CreatePollEntity poll);

    /// <summary>
    /// Update poll
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <param name="title">New title (null to keep current)</param>
    /// <param name="details">New details (null to keep current, empty string to clear)</param>
    /// <param name="startDate">New start date (null to keep current)</param>
    /// <param name="endDate">New end date (null to keep current)</param>
    /// <param name="isAnonymous">New anonymous status (null to keep current). Changing from anonymous to public resets all votes.</param>
    /// <returns>Updated poll</returns>
    Task<Poll> Update(Guid pollId, string? title, string? details, DateTimeOffset? startDate, DateTimeOffset? endDate, bool? isAnonymous);

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
    /// <returns>Updated poll with vote</returns>
    Task<Poll> Vote(Guid pollId, Guid optionId, Guid userId);

    /// <summary>
    /// Remove vote from the poll
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns>Updated poll without vote</returns>
    Task<Poll> Unvote(Guid pollId, Guid userId);
}
