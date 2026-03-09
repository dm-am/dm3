using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Shared.Comments;

/// <summary>
/// Unified comment service that routes to domain-specific implementations
/// </summary>
internal class CommentService : ICommentService
{
    private readonly IBlogCommentService _blogCommentService;
    private readonly IPublicationCommentService _publicationCommentService;
    private readonly ITopicCommentService _topicCommentService;
    private readonly IGameCommentService _gameCommentService;

    public CommentService(
        IBlogCommentService blogCommentService,
        IPublicationCommentService publicationCommentService,
        ITopicCommentService topicCommentService,
        IGameCommentService gameCommentService)
    {
        _blogCommentService = blogCommentService;
        _publicationCommentService = publicationCommentService;
        _topicCommentService = topicCommentService;
        _gameCommentService = gameCommentService;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingData Paging)> GetComments(
        CommentEntityType entityType,
        Guid entityId,
        PagingQuery paging,
        CancellationToken ct = default)
    {
        return entityType switch
        {
            CommentEntityType.Game => await GetGameComments(entityId, paging),
            CommentEntityType.Blog => await GetBlogComments(entityId, paging),
            CommentEntityType.Publication => await GetPublicationComments(entityId, paging),
            CommentEntityType.Topic => await GetTopicComments(entityId, paging),
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unknown comment entity type")
        };
    }

    /// <inheritdoc />
    public async Task<Comment?> GetComment(Guid commentId, CancellationToken ct = default)
    {
        // Try each domain until we find the comment
        try { return await _topicCommentService.Get(commentId); } catch { }
        try { return await _blogCommentService.Get(commentId); } catch { }
        try { return await _publicationCommentService.Get(commentId); } catch { }
        try { return await _gameCommentService.Get(commentId); } catch { }
        return null;
    }

    /// <inheritdoc />
    public async Task<Comment> CreateComment(
        CommentEntityType entityType,
        Guid entityId,
        CreateComment createComment,
        CancellationToken ct = default)
    {
        createComment.EntityId = entityId;

        return entityType switch
        {
            CommentEntityType.Game => await _gameCommentService.Create(createComment),
            CommentEntityType.Blog => await _blogCommentService.Create(createComment),
            CommentEntityType.Publication => await _publicationCommentService.Create(createComment),
            CommentEntityType.Topic => await _topicCommentService.Create(createComment),
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unknown comment entity type")
        };
    }

    /// <inheritdoc />
    public async Task<Comment> UpdateComment(Guid commentId, UpdateComment updateComment, CancellationToken ct = default)
    {
        updateComment.CommentId = commentId;

        // Try each domain - the authorization will fail if wrong domain
        try { return await _topicCommentService.Update(updateComment); } catch { }
        try { return await _blogCommentService.Update(updateComment); } catch { }
        try { return await _publicationCommentService.Update(updateComment); } catch { }
        return await _gameCommentService.Update(updateComment);
    }

    /// <inheritdoc />
    public async Task DeleteComment(Guid commentId, CancellationToken ct = default)
    {
        // Try each domain - the authorization will fail if wrong domain
        try { await _topicCommentService.Delete(commentId); return; } catch { }
        try { await _blogCommentService.Delete(commentId); return; } catch { }
        try { await _publicationCommentService.Delete(commentId); return; } catch { }
        await _gameCommentService.Delete(commentId);
    }

    private async Task<(IEnumerable<Comment> Comments, PagingData Paging)> GetGameComments(Guid gameId, PagingQuery paging)
    {
        var (comments, pagingResult) = await _gameCommentService.Get(gameId, paging);
        return (comments, ToPagingData(paging, pagingResult));
    }

    private async Task<(IEnumerable<Comment> Comments, PagingData Paging)> GetBlogComments(Guid blogId, PagingQuery paging)
    {
        var (comments, pagingResult) = await _blogCommentService.Get(blogId, paging);
        return (comments, ToPagingData(paging, pagingResult));
    }

    private async Task<(IEnumerable<Comment> Comments, PagingData Paging)> GetPublicationComments(Guid publicationId, PagingQuery paging)
    {
        var (comments, pagingResult) = await _publicationCommentService.Get(publicationId, paging);
        return (comments, ToPagingData(paging, pagingResult));
    }

    private async Task<(IEnumerable<Comment> Comments, PagingData Paging)> GetTopicComments(Guid topicId, PagingQuery paging)
    {
        var (comments, pagingResult) = await _topicCommentService.Get(topicId, paging);
        return (comments, ToPagingData(paging, pagingResult));
    }

    private static PagingData ToPagingData(PagingQuery query, PagingResult result)
    {
        return new PagingData(query, result.PageSize, result.TotalEntitiesCount);
    }
}
