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
        CommentQueries.Single(_dbContext, commentId, CommentQueries.PublicationIds(_dbContext), "DM.PublicationComments.Get", ct);

    /// <summary>
    /// The publication's half of the last-comment move.
    /// </summary>
    /// <remarks>
    /// What the statement says and why it says it that way is stated once, on
    /// <see cref="CommentWrites.Create{TOwner}" />. Only the table is spelled out
    /// here: EF refuses raw SQL whose text is not a compile-time constant, so the
    /// three owners cannot share one statement with the table interpolated into it.
    /// </remarks>
    private const string MoveLastCommentForwardSql =
        """
        UPDATE "Publications" SET "LastCommentId" = {1}
        WHERE "PublicationId" = {0}
          AND NOT EXISTS (
              SELECT 1 FROM "Comments" c
              WHERE c."CommentId" = "Publications"."LastCommentId"
                AND (c."CreatedUtc", c."CommentId") > ({2}, {1}))
        """;

    /// <inheritdoc />
    public async Task<(Comment comment, Guid commentId)> Create(CreateComment createComment, Guid authorId, Guid publicationId, CancellationToken ct = default)
    {
        var commentId = _guidFactory.Create();

        var comment = await CommentWrites.Create(
            _dbContext,
            _dbContext.Publications,
            p => p.PublicationId == publicationId,
            p => p.CommentCount,
            publicationId,
            new DbComment
            {
                CommentId = commentId,
                EntityId = publicationId,
                AuthorId = authorId,
                Text = createComment.Text,
                CreatedUtc = _dateTimeProvider.Now,
                IsRemoved = false
            },
            MoveLastCommentForwardSql,
            "DM.PublicationComments.Created",
            ct);

        return (comment, commentId);
    }

    /// <inheritdoc />
    public Task<Comment> Update(UpdatePublicationCommentEntity entity, CancellationToken ct = default)
        => CommentWrites.Update(_dbContext, _guidFactory, entity.CommentId, entity.Text,
            entity.EditorUserId, entity.LastUpdateUtc, "DM.PublicationComments.Updated", ct);

    /// <inheritdoc />
    public Task<PublicationCommentToDelete?> GetForDelete(
        Guid commentId, CancellationToken ct = default) =>
        CommentQueries.One(_dbContext, commentId, CommentQueries.PublicationIds(_dbContext))
            .TagWith("DM.PublicationComments.GetForDelete")
            .ProjectToPublicationCommentToDelete()
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<bool> Delete(DeletePublicationCommentEntity entity, CancellationToken ct = default) =>
        CommentWrites.Delete(
            _dbContext,
            _dbContext.Publications,
            p => p.PublicationId == entity.PublicationId,
            p => p.PublicationId == entity.PublicationId && p.LastCommentId == entity.CommentId,
            p => p.CommentCount,
            p => p.LastCommentId,
            entity.PublicationId,
            entity.CommentId,
            entity.DeletedByUserId,
            entity.DeletedUtc,
            "DM.PublicationComments.NewestCommentIdExcept",
            ct);
}
