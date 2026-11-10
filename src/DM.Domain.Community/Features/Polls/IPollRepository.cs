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
    /// <summary>
    /// Count polls matching query
    /// </summary>
    /// <param name="query">Query parameters</param>
    /// <returns>Number of polls</returns>
    Task<int> Count(PollsQuery query);

    Task<IEnumerable<Poll>> Get(PollsQuery query, PagingData pagingData);

    Task<Poll> Get(Guid id);

    Task<Poll> Create(CreatePollEntity poll);

    /// <param name="title">New title (null to keep current)</param>
    /// <param name="details">New details (null to keep current, empty string to clear)</param>
    /// <param name="startDate">New start date (null to keep current)</param>
    /// <param name="endDate">New end date (null to keep current)</param>
    /// <param name="isAnonymous">New anonymous status (null to keep current). Changing from anonymous to public resets all votes.</param>
    Task<Poll> Update(Guid pollId, string? title, string? details, DateTimeOffset? startDate, DateTimeOffset? endDate, bool? isAnonymous);

    /// <summary>
    /// Mark poll as removed
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    Task Delete(Guid pollId);

    /// <remarks>Null when this user has already voted in the poll: one vote per person is held by the record itself.</remarks>
    Task<Poll?> Vote(Guid pollId, Guid optionId, Guid userId);

    /// <summary>
    /// Remove vote from the poll
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns>Updated poll without vote</returns>
    Task<Poll> Unvote(Guid pollId, Guid userId);
}
