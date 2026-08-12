using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Comments;
using Microsoft.EntityFrameworkCore;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc cref="IBlogCommentRepository" />
internal class BlogCommentRepository : IBlogCommentRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BlogCommentRepository(
        DmDbContext dbContext,
        IMapper mapper,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid blogId, BlogCommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default) =>
        CommentQueries.Count(_dbContext, blogId, query, excludeUserIds, "DM.BlogComments.Count", ct);

    /// <inheritdoc />
    public Task<IEnumerable<Comment>> Get(Guid blogId, BlogCommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default) =>
        CommentQueries.Page(_dbContext, _mapper, blogId, query, paging, excludeUserIds, "DM.BlogComments.List", ct);

    /// <inheritdoc />
    public Task<Comment?> Get(Guid commentId, CancellationToken ct = default) =>
        CommentQueries.Single(_dbContext, _mapper, commentId, "DM.BlogComments.Get", ct);

    /// <inheritdoc />
    public async Task<(Comment comment, Guid commentId)> Create(CreateComment createComment, Guid authorId, Guid blogId, int newCommentCount, CancellationToken ct = default)
    {
        var commentId = Guid.NewGuid();
        var now = _dateTimeProvider.Now;

        var dbComment = new DbComment
        {
            CommentId = commentId,
            EntityId = blogId,
            AuthorId = authorId,
            Text = createComment.Text,
            CreatedUtc = now,
            IsRemoved = false
        };

        _dbContext.Comments.Add(dbComment);

        // Update blog comment count and last comment ID
        var blog = await _dbContext.Blogs.FindAsync([blogId], ct);
        if (blog != null)
        {
            blog.CommentCount = newCommentCount;
            blog.LastCommentId = commentId;
        }

        await _dbContext.SaveChangesAsync(ct);

        var comment = await _dbContext.Comments
            .TagWith("DM.BlogComments.Created")
            .Where(c => c.CommentId == commentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);

        return (comment, commentId);
    }

    /// <inheritdoc />
    public async Task<Comment> Update(UpdateBlogCommentEntity entity, CancellationToken ct = default)
    {
        var dbComment = await _dbContext.Comments.FindAsync([entity.CommentId], ct);
        if (dbComment != null)
        {
            dbComment.Text = entity.Text;
            // Modification tracking is handled via Edit history, not inline ModifiedUtc
            await _dbContext.SaveChangesAsync(ct);
        }

        return await _dbContext.Comments
            .TagWith("DM.BlogComments.Updated")
            .Where(c => c.CommentId == entity.CommentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BlogCommentToDelete?> GetForDelete(Guid commentId, CancellationToken ct = default)
    {
        var comment = await _dbContext.Comments
            .TagWith("DM.BlogComments.GetForDelete")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<BlogCommentToDelete>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        if (comment == null) return null;

        var blogInfo = await _dbContext.Blogs
            .Where(b => b.BlogId == comment.BlogId)
            .Select(b => new { b.CommentCount, b.LastCommentId })
            .FirstOrDefaultAsync(ct);

        if (blogInfo != null)
        {
            comment.BlogCommentCount = blogInfo.CommentCount;
            comment.IsLastComment = blogInfo.LastCommentId == commentId;
        }

        return comment;
    }

    /// <inheritdoc />
    public Task<Guid?> GetNewestCommentIdExcept(Guid blogId, Guid exceptCommentId, CancellationToken ct = default) =>
        CommentQueries.NewestExcept(
            _dbContext, blogId, exceptCommentId, "DM.BlogComments.NewestCommentIdExcept", ct);

    /// <inheritdoc />
    public async Task Delete(DeleteBlogCommentEntity entity, CancellationToken ct = default)
    {
        var dbComment = await _dbContext.Comments.FindAsync([entity.CommentId], ct);
        if (dbComment != null)
        {
            SoftDelete.Mark(dbComment, entity.DeletedByUserId, entity.DeletedUtc);
        }

        // Update blog comment count and last comment ID. The pointer moves only when
        // the row it points at is the one going away: the service computes
        // NewLastCommentId for the last comment and leaves it null for every other,
        // so assigning it unconditionally erased the pointer whenever somebody
        // deleted a comment from the middle of the discussion.
        var blog = await _dbContext.Blogs.FindAsync([entity.BlogId], ct);
        if (blog != null)
        {
            blog.CommentCount = entity.NewCommentCount;
            if (entity.NewLastCommentId.HasValue || blog.LastCommentId == entity.CommentId)
            {
                blog.LastCommentId = entity.NewLastCommentId;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
