using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;

    public BlogCommentRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid blogId, CommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default) =>
        CommentQueries.Count(_dbContext, blogId, query, excludeUserIds, "DM.BlogComments.Count", ct);

    /// <inheritdoc />
    public Task<IEnumerable<Comment>> Get(Guid blogId, CommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null, CancellationToken ct = default) =>
        CommentQueries.Page(_dbContext, blogId, query, paging, excludeUserIds, "DM.BlogComments.List", ct);

    /// <inheritdoc />
    public Task<Comment?> Get(Guid commentId, CancellationToken ct = default) =>
        CommentQueries.Single(_dbContext, commentId, CommentQueries.BlogIds(_dbContext), "DM.BlogComments.Get", ct);

    /// <summary>
    /// The blog's half of the last-comment move.
    /// </summary>
    /// <remarks>
    /// What the statement says and why it says it that way is stated once, on
    /// <see cref="CommentWrites.Create{TOwner}" />. Only the table is spelled out
    /// here: EF refuses raw SQL whose text is not a compile-time constant, so the
    /// three owners cannot share one statement with the table interpolated into it.
    /// </remarks>
    private const string MoveLastCommentForwardSql =
        """
        UPDATE "Blogs" SET "LastCommentId" = {1}
        WHERE "BlogId" = {0}
          AND NOT EXISTS (
              SELECT 1 FROM "Comments" c
              WHERE c."CommentId" = "Blogs"."LastCommentId"
                AND (c."CreatedUtc", c."CommentId") > ({2}, {1}))
        """;

    /// <inheritdoc />
    public async Task<(Comment comment, Guid commentId)> Create(CreateComment createComment, Guid authorId, Guid blogId, CancellationToken ct = default)
    {
        var commentId = _guidFactory.Create();

        var comment = await CommentWrites.Create(
            _dbContext,
            _dbContext.Blogs,
            b => b.BlogId == blogId,
            b => b.CommentCount,
            blogId,
            new DbComment
            {
                CommentId = commentId,
                EntityId = blogId,
                AuthorId = authorId,
                Text = createComment.Text,
                CreatedUtc = _dateTimeProvider.Now,
                IsRemoved = false
            },
            MoveLastCommentForwardSql,
            "DM.BlogComments.Created",
            ct);

        return (comment, commentId);
    }

    /// <inheritdoc />
    public Task<Comment> Update(UpdateBlogCommentEntity entity, CancellationToken ct = default)
        => CommentWrites.Update(_dbContext, _guidFactory, entity.CommentId, entity.Text,
            entity.EditorUserId, entity.LastUpdateUtc, "DM.BlogComments.Updated", ct);

    /// <inheritdoc />
    public Task<BlogCommentToDelete?> GetForDelete(Guid commentId, CancellationToken ct = default) =>
        CommentQueries.One(_dbContext, commentId, CommentQueries.BlogIds(_dbContext))
            .TagWith("DM.BlogComments.GetForDelete")
            .ProjectToBlogCommentToDelete()
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<bool> Delete(DeleteBlogCommentEntity entity, CancellationToken ct = default) =>
        CommentWrites.Delete(
            _dbContext,
            _dbContext.Blogs,
            b => b.BlogId == entity.BlogId,
            b => b.BlogId == entity.BlogId && b.LastCommentId == entity.CommentId,
            b => b.CommentCount,
            b => b.LastCommentId,
            entity.BlogId,
            entity.CommentId,
            entity.DeletedByUserId,
            entity.DeletedUtc,
            "DM.BlogComments.NewestCommentIdExcept",
            ct);
}
