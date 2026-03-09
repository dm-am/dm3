using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Forum.Features.Topics;

/// <summary>
/// Repository for forum topics (unified CRUD operations)
/// </summary>
public interface ITopicRepository
{
    // --- READ ---

    /// <summary>
    /// Get number of non-attached topics in a board
    /// </summary>
    /// <param name="boardId">Board identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task<int> Count(Guid boardId, CancellationToken ct = default);

    /// <summary>
    /// Get list of board topics
    /// </summary>
    /// <param name="boardId">Board identifier</param>
    /// <param name="pagingData">Paging data</param>
    /// <param name="attached">Select attached/not attached topics exclusively</param>
    /// <param name="ct">Cancellation token</param>
    Task<IEnumerable<Topic>> Get(Guid boardId, PagingData? pagingData, bool attached, CancellationToken ct = default);

    /// <summary>
    /// Get single topic by identifier
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="accessPolicy">Board access policy</param>
    /// <param name="ct">Cancellation token</param>
    Task<Topic?> Get(Guid topicId, BoardAccessPolicy accessPolicy, CancellationToken ct = default);

    // --- WRITE ---

    /// <summary>
    /// Create new topic
    /// </summary>
    /// <param name="createTopic">Topic creation data</param>
    /// <param name="authorId">Author user ID</param>
    /// <param name="boardId">Board ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created topic DTO</returns>
    Task<Topic> Create(CreateTopicEntity createTopic, Guid authorId, Guid boardId, CancellationToken ct = default);

    /// <summary>
    /// Update existing topic
    /// </summary>
    /// <param name="updateTopic">Topic update data</param>
    /// <param name="boardId">New board ID (if changed)</param>
    /// <returns>Updated topic DTO</returns>
    Task<Topic> Update(UpdateTopicEntity updateTopic, Guid? boardId = null);

    /// <summary>
    /// Soft delete a topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    Task Delete(Guid topicId);
}

/// <summary>
/// Internal DTO for topic creation (repository layer)
/// </summary>
public class CreateTopicEntity
{
    /// <summary>
    /// Topic title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Topic text
    /// </summary>
    public string Text { get; set; } = null!;
}

/// <summary>
/// Internal DTO for topic update (repository layer)
/// </summary>
public class UpdateTopicEntity
{
    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// Updated title (optional)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Updated text (optional)
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Updated closed status (optional)
    /// </summary>
    public bool? IsClosed { get; set; }

    /// <summary>
    /// Updated attached status (optional)
    /// </summary>
    public bool? IsAttached { get; set; }
}
