using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Topics;

/// <summary>
/// Service for topic operations
/// </summary>
public interface ITopicService
{
    /// <summary>
    /// Create new topic
    /// </summary>
    /// <param name="createTopic">Create topic model</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created topic</returns>
    Task<Topic> CreateAsync(CreateTopic createTopic, CancellationToken ct = default);

    /// <summary>
    /// Get topic by id
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Topic</returns>
    Task<Topic> GetAsync(Guid topicId, CancellationToken ct = default);

    /// <summary>
    /// Get topics page of certain board by its title
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <param name="query">Paging query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Pair of topics list and paging data</returns>
    Task<(IEnumerable<Topic> topics, PagingResult paging)> GetListAsync(
        string boardTitle, PagingQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get attached topics of certain board by its title
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Attached topics</returns>
    Task<IEnumerable<Topic>> GetAttachedAsync(string boardTitle, CancellationToken ct = default);

    /// <summary>
    /// Update existing topic
    /// </summary>
    /// <param name="updateTopic">Update topic model</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated topic</returns>
    Task<Topic> UpdateAsync(UpdateTopic updateTopic, CancellationToken ct = default);

    /// <summary>
    /// Remove existing topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteAsync(Guid topicId, CancellationToken ct = default);
}
