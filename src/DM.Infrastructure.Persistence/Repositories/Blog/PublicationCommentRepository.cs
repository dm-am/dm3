using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using DbComment = DM.Infrastructure.Persistence.Entities.CrossDomain.Comment;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc cref="IPublicationCommentRepository" />
internal class PublicationCommentRepository : IPublicationCommentRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public PublicationCommentRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid publicationId, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default)
    {
        var query = _dbContext.Comments
            .TagWith("DM.PublicationComments.Count")
            .Where(c => !c.IsRemoved && c.EntityId == publicationId);

        if (excludeUserIds is { Count: > 0 })
        {
            query = query.Where(c => !excludeUserIds.Contains(c.AuthorId));
        }

        return query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Comment>> Get(Guid publicationId, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default)
    {
        var query = _dbContext.Comments
            .TagWith("DM.PublicationComments.List")
            .Where(c => !c.IsRemoved && c.EntityId == publicationId);

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
            .TagWith("DM.PublicationComments.Get")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<(Comment comment, Guid commentId)> Create(CreateComment createComment, Guid authorId, Guid publicationId, int newCommentCount, CancellationToken ct = default)
    {
        var commentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var dbComment = new DbComment
        {
            CommentId = commentId,
            EntityId = publicationId,
            AuthorId = authorId,
            Text = createComment.Text,
            CreatedUtc = now,
            IsRemoved = false
        };

        _dbContext.Comments.Add(dbComment);

        // Update publication comment count and last comment ID
        var publication = await _dbContext.Publications.FindAsync([publicationId], ct);
        if (publication != null)
        {
            publication.CommentCount = newCommentCount;
            publication.LastCommentId = commentId;
        }

        await _dbContext.SaveChangesAsync(ct);

        var comment = await _dbContext.Comments
            .TagWith("DM.PublicationComments.Created")
            .Where(c => c.CommentId == commentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);

        return (comment, commentId);
    }

    /// <inheritdoc />
    public async Task<Comment> Update(UpdatePublicationCommentEntity entity, CancellationToken ct = default)
    {
        var dbComment = await _dbContext.Comments.FindAsync([entity.CommentId], ct);
        if (dbComment != null)
        {
            dbComment.Text = entity.Text;
            dbComment.ModifiedUtc = entity.LastUpdateUtc;
            await _dbContext.SaveChangesAsync(ct);
        }

        return await _dbContext.Comments
            .TagWith("DM.PublicationComments.Updated")
            .Where(c => c.CommentId == entity.CommentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task<PublicationCommentToDelete?> GetForDelete(Guid commentId, CancellationToken ct = default)
    {
        var comment = await _dbContext.Comments
            .TagWith("DM.PublicationComments.GetForDelete")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<PublicationCommentToDelete>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        if (comment == null) return null;

        var publicationInfo = await _dbContext.Publications
            .Where(p => p.PublicationId == comment.PublicationId)
            .Select(p => new { p.CommentCount, p.LastCommentId })
            .FirstOrDefaultAsync(ct);

        if (publicationInfo != null)
        {
            comment.PublicationCommentCount = publicationInfo.CommentCount;
            comment.IsLastComment = publicationInfo.LastCommentId == commentId;
        }

        return comment;
    }

    /// <inheritdoc />
    public async Task<Guid?> GetSecondLastCommentId(Guid publicationId, CancellationToken ct = default)
    {
        return await _dbContext.Comments
            .TagWith("DM.PublicationComments.SecondLastCommentId")
            .Where(c => !c.IsRemoved && c.EntityId == publicationId)
            .OrderByDescending(c => c.CreatedUtc)
            .Skip(1)
            .Select(c => (Guid?)c.CommentId)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task Delete(DeletePublicationCommentEntity entity, CancellationToken ct = default)
    {
        var dbComment = await _dbContext.Comments.FindAsync([entity.CommentId], ct);
        if (dbComment != null)
        {
            dbComment.IsRemoved = true;
        }

        // Update publication comment count and last comment ID
        var publication = await _dbContext.Publications.FindAsync([entity.PublicationId], ct);
        if (publication != null)
        {
            publication.CommentCount = entity.NewCommentCount;
            publication.LastCommentId = entity.NewLastCommentId;
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
