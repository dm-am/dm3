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
    /// Get topic by board alias and topic number
    /// </summary>
    /// <param name="boardAlias">Board URL alias</param>
    /// <param name="topicNumber">Topic number within board</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Topic</returns>
    Task<Topic> GetByBoardAndNumberAsync(string boardAlias, int topicNumber, CancellationToken ct = default);

    /// <summary>
    /// Get topics page of certain board by its title with filtering and sorting
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <param name="query">Filtering and paging query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Topics list and paging data (paging is null for attached-only queries)</returns>
    Task<(IEnumerable<Topic> topics, PagingResult? paging)> GetListAsync(
        string boardTitle, TopicsQuery query, CancellationToken ct = default);

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

    /// <summary>
    /// Reorder pinned topics in a board
    /// </summary>
    /// <param name="boardTitle">Board title or alias</param>
    /// <param name="topicIds">Topic IDs in desired order (first = top)</param>
    /// <param name="ct">Cancellation token</param>
    Task ReorderPinnedAsync(string boardTitle, IReadOnlyList<Guid> topicIds, CancellationToken ct = default);
}
