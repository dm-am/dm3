using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Comments;
using Microsoft.EntityFrameworkCore;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc cref="IPublicationCommentRepository" />
internal class PublicationCommentRepository : IPublicationCommentRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;

    public PublicationCommentRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid publicationId, CommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default) =>
        CommentQueries.Count(_dbContext, publicationId, query, excludeUserIds, "DM.PublicationComments.Count", ct);

    /// <inheritdoc />
    public Task<IEnumerable<Comment>> Get(Guid publicationId, CommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default) =>
        CommentQueries.Page(_dbContext, publicationId, query, paging, excludeUserIds, "DM.PublicationComments.List", ct);

    /// <inheritdoc />
    public Task<Comment?> Get(Guid commentId, CancellationToken ct = default) =>
        CommentQueries.Single(_dbContext, commentId, "DM.PublicationComments.Get", ct);

    /// <inheritdoc />
    public async Task<(Comment comment, Guid commentId)> Create(CreateComment createComment, Guid authorId, Guid publicationId, int newCommentCount, CancellationToken ct = default)
    {
        var commentId = _guidFactory.Create();
        var now = _dateTimeProvider.Now;

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
            .ProjectToComment()
            .FirstAsync(ct);

        return (comment, commentId);
    }

    /// <inheritdoc />
    public Task<Comment> Update(UpdatePublicationCommentEntity entity, CancellationToken ct = default)
        => CommentWrites.Update(_dbContext, _guidFactory, entity.CommentId, entity.Text,
            entity.EditorUserId, entity.LastUpdateUtc, "DM.PublicationComments.Updated", ct);

    /// <inheritdoc />
    public async Task<PublicationCommentToDelete?> GetForDelete(Guid commentId, CancellationToken ct = default)
    {
        var comment = await _dbContext.Comments
            .TagWith("DM.PublicationComments.GetForDelete")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectToPublicationCommentToDelete()
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
    public Task<Guid?> GetNewestCommentIdExcept(
        Guid publicationId, Guid exceptCommentId, CancellationToken ct = default) =>
        CommentQueries.NewestExcept(
            _dbContext, publicationId, exceptCommentId, "DM.PublicationComments.NewestCommentIdExcept", ct);

    /// <inheritdoc />
    public async Task Delete(DeletePublicationCommentEntity entity, CancellationToken ct = default)
    {
        var dbComment = await _dbContext.Comments.FindAsync([entity.CommentId], ct);
        if (dbComment != null)
        {
            SoftDelete.Mark(dbComment, entity.DeletedByUserId, entity.DeletedUtc);
        }

        // Update publication comment count and last comment ID. The pointer moves
        // only when the row it points at is the one going away: the service computes
        // NewLastCommentId for the last comment and leaves it null for every other,
        // so assigning it unconditionally erased the pointer whenever somebody
        // deleted a comment from the middle of the discussion.
        var publication = await _dbContext.Publications.FindAsync([entity.PublicationId], ct);
        if (publication != null)
        {
            publication.CommentCount = entity.NewCommentCount;
            if (entity.NewLastCommentId.HasValue || publication.LastCommentId == entity.CommentId)
            {
                publication.LastCommentId = entity.NewLastCommentId;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
