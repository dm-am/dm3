using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Forum.Topics;

/// <summary>
/// API service for forum topics
/// </summary>
public interface ITopicApiService
{
    /// <summary>
    /// Get forum topics
    /// </summary>
    /// <param name="forumId">Forum identifier</param>
    /// <param name="query">Search query</param>
    /// <returns>Envelope of topics list</returns>
    Task<ListEnvelope<Topic>> Get(string forumId, TopicsQuery query);

    /// <summary>
    /// Get topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <returns>Envelope of topic</returns>
    Task<Envelope<Topic>> Get(Guid topicId);

    /// <summary>
    /// Get topic by board alias and topic number
    /// </summary>
    /// <param name="boardAlias">Board URL alias</param>
    /// <param name="topicNumber">Topic number within board</param>
    /// <returns>Envelope of topic</returns>
    Task<Envelope<Topic>> GetByBoardAndNumber(string boardAlias, int topicNumber);

    /// <summary>
    /// Create new topic
    /// </summary>
    /// <param name="forumId">Forum identifier</param>
    /// <param name="request">Topic creation request</param>
    /// <returns>Envelope of created topic</returns>
    Task<Envelope<Topic>> Create(string forumId, CreateTopicRequest request);

    /// <summary>
    /// Updates topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="topic">Topic model</param>
    /// <returns>Envelop of updated topic</returns>
    Task<Envelope<Topic>> Update(Guid topicId, Topic topic);

    /// <summary>
    /// Removes topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    Task Delete(Guid topicId);

    /// <summary>
    /// Reorders pinned topics in a board
    /// </summary>
    /// <param name="boardId">Board identifier</param>
    /// <param name="request">Reorder request with topic IDs</param>
    Task ReorderPinned(string boardId, ReorderPinnedRequest request);
}
