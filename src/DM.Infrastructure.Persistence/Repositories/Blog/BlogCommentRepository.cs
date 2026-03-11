using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc cref="IBlogCommentRepository" />
internal class BlogCommentRepository : IBlogCommentRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public BlogCommentRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid blogId, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default)
    {
        var query = _dbContext.Comments
            .TagWith("DM.BlogComments.Count")
            .Where(c => !c.IsRemoved && c.EntityId == blogId);

        if (excludeUserIds is { Count: > 0 })
        {
            query = query.Where(c => !excludeUserIds.Contains(c.AuthorId));
        }

        return query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Comment>> Get(Guid blogId, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default)
    {
        var query = _dbContext.Comments
            .TagWith("DM.BlogComments.List")
            .Where(c => !c.IsRemoved && c.EntityId == blogId);

        if (excludeUserIds is { Count: > 0 })
        {
            query = query.Where(c => !excludeUserIds.Contains(c.AuthorId));
        }

        return await query
            .OrderBy(c => c.CreatedUtc)
            .Page(paging)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public Task<Comment?> Get(Guid commentId, CancellationToken ct = default)
    {
        return _dbContext.Comments
            .TagWith("DM.BlogComments.Get")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<(Comment comment, Guid commentId)> Create(CreateComment createComment, Guid authorId, Guid blogId, int newCommentCount, CancellationToken ct = default)
    {
        var commentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

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
            dbComment.ModifiedUtc = entity.LastUpdateUtc;
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
    public async Task<Guid?> GetSecondLastCommentId(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.Comments
            .TagWith("DM.BlogComments.SecondLastCommentId")
            .Where(c => !c.IsRemoved && c.EntityId == blogId)
            .OrderByDescending(c => c.CreatedUtc)
            .Skip(1)
            .Select(c => (Guid?)c.CommentId)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task Delete(DeleteBlogCommentEntity entity, CancellationToken ct = default)
    {
        var dbComment = await _dbContext.Comments.FindAsync([entity.CommentId], ct);
        if (dbComment != null)
        {
            dbComment.IsRemoved = true;
        }

        // Update blog comment count and last comment ID
        var blog = await _dbContext.Blogs.FindAsync([entity.BlogId], ct);
        if (blog != null)
        {
            blog.CommentCount = entity.NewCommentCount;
            blog.LastCommentId = entity.NewLastCommentId;
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
