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
    public async Task<(IEnumerable<Comment> Comments, PagingData Paging)> GetCommentsAsync(
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
    public async Task<Comment?> GetCommentAsync(Guid commentId, CancellationToken ct = default)
    {
        // Try each domain until we find the comment
        try { return await _topicCommentService.GetAsync(commentId); } catch { }
        try { return await _blogCommentService.GetAsync(commentId); } catch { }
        try { return await _publicationCommentService.GetAsync(commentId); } catch { }
        try { return await _gameCommentService.GetAsync(commentId); } catch { }
        return null;
    }

    /// <inheritdoc />
    public async Task<Comment> CreateCommentAsync(
        CommentEntityType entityType,
        Guid entityId,
        CreateComment createComment,
        CancellationToken ct = default)
    {
        createComment.EntityId = entityId;

        return entityType switch
        {
            CommentEntityType.Game => await _gameCommentService.CreateAsync(createComment),
            CommentEntityType.Blog => await _blogCommentService.CreateAsync(createComment),
            CommentEntityType.Publication => await _publicationCommentService.CreateAsync(createComment),
            CommentEntityType.Topic => await _topicCommentService.CreateAsync(createComment),
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unknown comment entity type")
        };
    }

    /// <inheritdoc />
    public async Task<Comment> UpdateCommentAsync(Guid commentId, UpdateComment updateComment, CancellationToken ct = default)
    {
        updateComment.CommentId = commentId;

        // Try each domain - the authorization will fail if wrong domain
        try { return await _topicCommentService.UpdateAsync(updateComment); } catch { }
        try { return await _blogCommentService.UpdateAsync(updateComment); } catch { }
        try { return await _publicationCommentService.UpdateAsync(updateComment); } catch { }
        return await _gameCommentService.UpdateAsync(updateComment);
    }

    /// <inheritdoc />
    public async Task DeleteCommentAsync(Guid commentId, CancellationToken ct = default)
    {
        // Try each domain - the authorization will fail if wrong domain
        try { await _topicCommentService.DeleteAsync(commentId); return; } catch { }
        try { await _blogCommentService.DeleteAsync(commentId); return; } catch { }
        try { await _publicationCommentService.DeleteAsync(commentId); return; } catch { }
        await _gameCommentService.DeleteAsync(commentId);
    }

    private async Task<(IEnumerable<Comment> Comments, PagingData Paging)> GetGameComments(Guid gameId, PagingQuery paging)
    {
        var query = new GameCommentsQuery { Skip = paging.Skip, Take = paging.Take };
        var (comments, pagingResult) = await _gameCommentService.GetAsync(gameId, query);
        return (comments, ToPagingData(paging, pagingResult));
    }

    private async Task<(IEnumerable<Comment> Comments, PagingData Paging)> GetBlogComments(Guid blogId, PagingQuery paging)
    {
        var query = new BlogCommentsQuery { Skip = paging.Skip, Take = paging.Take };
        var (comments, pagingResult) = await _blogCommentService.GetAsync(blogId, query);
        return (comments, ToPagingData(paging, pagingResult));
    }

    private async Task<(IEnumerable<Comment> Comments, PagingData Paging)> GetPublicationComments(Guid publicationId, PagingQuery paging)
    {
        var query = new PublicationCommentsQuery { Skip = paging.Skip, Take = paging.Take };
        var (comments, pagingResult) = await _publicationCommentService.GetAsync(publicationId, query);
        return (comments, ToPagingData(paging, pagingResult));
    }

    private async Task<(IEnumerable<Comment> Comments, PagingData Paging)> GetTopicComments(Guid topicId, PagingQuery paging)
    {
        var query = new CommentsQuery { Skip = paging.Skip, Take = paging.Take };
        var (comments, pagingResult) = await _topicCommentService.GetAsync(topicId, query);
        return (comments, ToPagingData(paging, pagingResult));
    }

    private static PagingData ToPagingData(PagingQuery query, PagingResult result)
    {
        return new PagingData(query, result.PageSize, result.TotalEntitiesCount);
    }
}
