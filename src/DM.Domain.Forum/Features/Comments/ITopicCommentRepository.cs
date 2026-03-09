using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Comments;

/// <summary>
/// Repository for topic comments
/// </summary>
public interface ITopicCommentRepository
{
    /// <summary>
    /// Count comments of the topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from count</param>
    Task<int> Count(Guid topicId, IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get comments list of the topic
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="paging">Paging data</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    Task<IEnumerable<Comment>> Get(Guid topicId, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get single comment by its identifier
    /// </summary>
    Task<Comment?> Get(Guid commentId);

    /// <summary>
    /// Create comment for topic
    /// </summary>
    /// <param name="createComment">Comment creation data</param>
    /// <returns>Created comment</returns>
    Task<Comment> Create(CreateTopicCommentEntity createComment);

    /// <summary>
    /// Update comment
    /// </summary>
    /// <param name="updateComment">Comment update data</param>
    /// <returns>Updated comment</returns>
    Task<Comment> Update(UpdateTopicCommentEntity updateComment);

    /// <summary>
    /// Get comment for deletion with topic info
    /// </summary>
    Task<TopicCommentToDelete?> GetForDelete(Guid commentId);

    /// <summary>
    /// Gets second last comment identifier of the topic
    /// </summary>
    Task<Guid?> GetSecondLastCommentId(Guid topicId);

    /// <summary>
    /// Delete comment (soft delete)
    /// </summary>
    /// <param name="deleteComment">Comment deletion data</param>
    Task Delete(DeleteTopicCommentEntity deleteComment);
}

/// <summary>
/// DTO for creating a topic comment (repository layer)
/// </summary>
public class CreateTopicCommentEntity
{
    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// Comment author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Comment text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// New comment count for the topic
    /// </summary>
    public int NewCommentCount { get; set; }
}

/// <summary>
/// DTO for updating a topic comment (repository layer)
/// </summary>
public class UpdateTopicCommentEntity
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Updated comment text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Last edit timestamp
    /// </summary>
    public DateTimeOffset LastUpdateUtc { get; set; }
}

/// <summary>
/// DTO to remove topic comment
/// </summary>
public class TopicCommentToDelete : Comment
{
    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId => EntityId;

    /// <summary>
    /// Current comment count of the topic
    /// </summary>
    public int TopicCommentCount { get; set; }

    /// <summary>
    /// Tells if the comment is last comment of the topic
    /// </summary>
    public bool IsLastComment { get; set; }
}

/// <summary>
/// DTO for deleting a topic comment (repository layer)
/// </summary>
public class DeleteTopicCommentEntity
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// New comment count after deletion
    /// </summary>
    public int NewCommentCount { get; set; }

    /// <summary>
    /// New last comment ID (if this was the last comment)
    /// </summary>
    public Guid? NewLastCommentId { get; set; }
}
