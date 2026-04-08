using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Comments;

/// <summary>
/// Service for managing comments on forum topics
/// </summary>
public interface ITopicCommentService
{
    /// <summary>
    /// Create a comment on a topic
    /// </summary>
    Task<Comment> CreateAsync(CreateComment createComment);

    /// <summary>
    /// Get comments for a topic with filtering and sorting
    /// </summary>
    /// <param name="topicId">Topic identifier</param>
    /// <param name="query">Query with paging, search, author filter, and sorting</param>
    /// <param name="excludeUserIds">Optional user IDs to exclude from results</param>
    Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid topicId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null);

    /// <summary>
    /// Get a single comment by ID
    /// </summary>
    Task<Comment> GetAsync(Guid commentId);

    /// <summary>
    /// Update a comment
    /// </summary>
    Task<Comment> UpdateAsync(UpdateComment updateComment);

    /// <summary>
    /// Delete a comment
    /// </summary>
    Task DeleteAsync(Guid commentId);

    /// <summary>
    /// Mark all topic comments as read
    /// </summary>
    Task MarkAsReadAsync(Guid topicId);

    /// <summary>
    /// Mark all comments in a board as read
    /// </summary>
    Task MarkBoardAsReadAsync(string boardTitle);

    /// <summary>
    /// Mark all forum comments as read (across all boards)
    /// </summary>
    Task MarkAllAsReadAsync();
}
