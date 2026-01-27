using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Forum.Dto.Output;

namespace DM.Services.Forum.BusinessProcesses.Topics.Reading;

/// <summary>
/// Board topics storage
/// </summary>
internal interface ITopicReadingRepository
{
    /// <summary>
    /// Get number of board topics
    /// </summary>
    /// <param name="boardId">Board identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<int> Count(Guid boardId, CancellationToken ct = default);

    /// <summary>
    /// Get list of board topics
    /// </summary>
    /// <param name="boardId">Board identifier</param>
    /// <param name="pagingData">Paging data</param>
    /// <param name="attached">Select attached/not attached topics exclusively</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<IEnumerable<Topic>> Get(Guid boardId, PagingData pagingData, bool attached, CancellationToken ct = default);

    /// <summary>
    /// Get topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="accessPolicy">Board access policy</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<Topic> Get(Guid topicId, BoardAccessPolicy accessPolicy, CancellationToken ct = default);
}