using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Services.Forum.Dto.Output;

namespace DM.Services.Forum.BusinessProcesses.Topics.Reading;

/// <summary>
/// Service for reading board topics
/// </summary>
public interface ITopicReadingService
{
    /// <summary>
    /// Get topics page of certain board by its title
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <param name="query">Paging query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Pair of topics list and paging data</returns>
    Task<(IEnumerable<Topic> topics, PagingResult paging)> GetTopicsList(string boardTitle, PagingQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get attached topics of certain board by its title
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<IEnumerable<Topic>> GetAttachedTopics(string boardTitle, CancellationToken ct = default);

    /// <summary>
    /// Get topic by id
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<Topic> GetTopic(Guid topicId, CancellationToken ct = default);
}