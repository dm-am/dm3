using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Domain.Forum.Features.Comments;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <inheritdoc />
internal class TopicCommentRepository : ITopicCommentRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TopicCommentRepository(
        DmDbContext dbContext,
        IMapper mapper,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid topicId, CommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var dbQuery = _dbContext.Comments
            .TagWith("DM.TopicComments.Count")
            .Where(c => !c.IsRemoved && c.EntityId == topicId);

        dbQuery = ApplyFilters(dbQuery, query, excludeUserIds);

        return dbQuery.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Comment>> Get(Guid topicId, CommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var dbQuery = _dbContext.Comments
            .TagWith("DM.TopicComments.List")
            .Where(c => !c.IsRemoved && c.EntityId == topicId);

        dbQuery = ApplyFilters(dbQuery, query, excludeUserIds);

        var orderedQuery = ApplySorting(dbQuery, query, _dbContext);

        return await orderedQuery
            .Page(paging)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    private static IQueryable<Entities.Shared.Comment> ApplyFilters(
        IQueryable<Entities.Shared.Comment> query,
        CommentsQuery commentsQuery,
        IReadOnlyCollection<Guid>? excludeUserIds)
    {
        // Exclude blocked users
        if (excludeUserIds is { Count: > 0 })
        {
            query = query.Where(c => !excludeUserIds.Contains(c.AuthorId));
        }

        // Filter by authors (OR logic)
        if (commentsQuery.Authors is { Count: > 0 })
        {
            var authorNames = commentsQuery.Authors.Select(a => a.ToLowerInvariant()).ToArray();
            query = query.Where(c => c.Author != null && authorNames.Contains(c.Author.Username.ToLower()));
        }

        // Filter by created date range
        if (commentsQuery.CreatedFromUtc.HasValue)
        {
            query = query.Where(c => c.CreatedUtc >= commentsQuery.CreatedFromUtc.Value);
        }

        if (commentsQuery.CreatedToUtc.HasValue)
        {
            query = query.WhereAtOrBefore(c => c.CreatedUtc, commentsQuery.CreatedToUtc.Value);
        }

        // Search by text content
        if (!string.IsNullOrWhiteSpace(commentsQuery.Search))
        {
            var searchLower = commentsQuery.Search.ToLowerInvariant();
            query = query.Where(c => c.Text.ToLower().Contains(searchLower));
        }

        return query;
    }

    private static IOrderedQueryable<Entities.Shared.Comment> ApplySorting(
        IQueryable<Entities.Shared.Comment> query,
        CommentsQuery commentsQuery,
        DmDbContext dbContext)
    {
        var sortBy = commentsQuery.SortBy?.ToLowerInvariant() ?? "created";
        var isDescending = string.Equals(commentsQuery.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy switch
        {
            "likes" => isDescending
                ? query.OrderByDescending(c => dbContext.Likes.Count(l =>
                    !l.IsRemoved &&
                    l.EntityId == c.CommentId &&
                    l.EntityType == Domain.Core.Enums.LikeEntityType.Comment))
                : query.OrderBy(c => dbContext.Likes.Count(l =>
                    !l.IsRemoved &&
                    l.EntityId == c.CommentId &&
                    l.EntityType == Domain.Core.Enums.LikeEntityType.Comment)),
            _ => isDescending // "created" or default
                ? query.OrderByDescending(c => c.CreatedUtc)
                : query.OrderBy(c => c.CreatedUtc)
        };
    }

    /// <inheritdoc />
    public Task<Comment?> Get(Guid commentId)
    {
        return _dbContext.Comments
            .TagWith("DM.TopicComments.Get")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<FirstUnreadComment?> FindFirstUnread(Guid topicId, DateTimeOffset lastReadUtc,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var comments = VisibleComments(topicId, excludeUserIds);

        var firstUnread = await comments
            .TagWith("DM.TopicComments.FirstUnread")
            .Where(c => c.CreatedUtc > lastReadUtc)
            .OrderBy(c => c.CreatedUtc)
            .Select(c => new { c.CommentId, c.CreatedUtc })
            .FirstOrDefaultAsync();

        return firstUnread == null
            ? null
            : await Position(comments, firstUnread.CommentId, firstUnread.CreatedUtc);
    }

    /// <inheritdoc />
    public async Task<FirstUnreadComment?> GetLastComment(Guid topicId,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var comments = VisibleComments(topicId, excludeUserIds);

        var lastComment = await comments
            .TagWith("DM.TopicComments.LastComment")
            .OrderByDescending(c => c.CreatedUtc)
            .Select(c => new { c.CommentId, c.CreatedUtc })
            .FirstOrDefaultAsync();

        return lastComment == null
            ? null
            : await Position(comments, lastComment.CommentId, lastComment.CreatedUtc);
    }

    /// <summary>
    /// Comments of the topic as this reader is shown them. Counting a position
    /// over any other set would send him to a page the comment is not on.
    /// </summary>
    private IQueryable<Entities.Shared.Comment> VisibleComments(
        Guid topicId, IReadOnlyCollection<Guid>? excludeUserIds)
    {
        var query = _dbContext.Comments.Where(c => !c.IsRemoved && c.EntityId == topicId);

        return excludeUserIds is { Count: > 0 }
            ? query.Where(c => !excludeUserIds.Contains(c.AuthorId))
            : query;
    }

    /// <summary>
    /// The comment's 1-based place in the order the discussion is paged by,
    /// oldest first, which is the order the list renders without a sort.
    /// </summary>
    private static async Task<FirstUnreadComment> Position(
        IQueryable<Entities.Shared.Comment> comments, Guid commentId, DateTimeOffset createdUtc)
    {
        return new FirstUnreadComment
        {
            CommentId = commentId,
            CommentNumber = await comments
                .TagWith("DM.TopicComments.Position")
                .CountAsync(c => c.CreatedUtc <= createdUtc)
        };
    }

    /// <inheritdoc />
    public async Task<Comment> Create(CreateTopicCommentEntity createComment)
    {
        var commentId = _guidFactory.Create();
        var now = _dateTimeProvider.Now;

        var dbComment = new Entities.Shared.Comment
        {
            CommentId = commentId,
            EntityId = createComment.TopicId,
            AuthorId = createComment.AuthorId,
            Text = createComment.Text,
            CreatedUtc = now,
            IsRemoved = false
        };

        _dbContext.Comments.Add(dbComment);

        // Update topic comment count and last comment ID
        var topic = await _dbContext.Topics.FindAsync(createComment.TopicId);
        if (topic != null)
        {
            topic.LastCommentId = commentId;
        }

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Comments
            .TagWith("DM.TopicComments.Created")
            .Where(c => c.CommentId == commentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<Comment> Update(UpdateTopicCommentEntity updateComment)
    {
        var dbComment = await _dbContext.Comments.FindAsync(updateComment.CommentId);
        if (dbComment != null)
        {
            dbComment.Text = updateComment.Text;
            // Modification tracking is handled via Edit history, not inline ModifiedUtc
            await _dbContext.SaveChangesAsync();
        }

        return await _dbContext.Comments
            .TagWith("DM.TopicComments.Updated")
            .Where(c => c.CommentId == updateComment.CommentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<TopicCommentToDelete?> GetForDelete(Guid commentId)
    {
        var comment = await _dbContext.Comments
            .TagWith("DM.TopicComments.GetForDelete")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<TopicCommentToDelete>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (comment == null) return null;

        // Get topic info: comment count and last comment id
        var topicInfo = await _dbContext.Topics
            .Where(t => t.TopicId == comment.EntityId)
            .Select(t => new { t.LastCommentId })
            .FirstOrDefaultAsync();

        if (topicInfo != null)
        {
            comment.IsLastComment = topicInfo.LastCommentId == commentId;
        }

        return comment;
    }

    /// <inheritdoc />
    public async Task<Guid?> GetSecondLastCommentId(Guid topicId)
    {
        return await _dbContext.Comments
            .TagWith("DM.TopicComments.SecondLastCommentId")
            .Where(c => !c.IsRemoved && c.EntityId == topicId)
            .OrderByDescending(c => c.CreatedUtc)
            .Skip(1)
            .Select(c => (Guid?)c.CommentId)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task Delete(DeleteTopicCommentEntity deleteComment)
    {
        var dbComment = await _dbContext.Comments.FindAsync(deleteComment.CommentId);
        if (dbComment != null)
        {
            dbComment.IsRemoved = true;
        }

        // Update topic comment count and last comment ID
        var topic = await _dbContext.Topics.FindAsync(deleteComment.TopicId);
        if (topic != null)
        {
            topic.LastCommentId = deleteComment.NewLastCommentId;
        }

        await _dbContext.SaveChangesAsync();
    }
}
