using System;
using DM.Domain.Core.Abstractions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Comments;
using Microsoft.EntityFrameworkCore;
using CommentDal = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class GameCommentRepository : IGameCommentRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;

    public GameCommentRepository(
        DmDbContext dbContext,
        IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid gameId, CommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null) =>
        CommentQueries.Count(_dbContext, gameId, query, excludeUserIds, "DM.GameComments.Count");

    /// <inheritdoc />
    public Task<IEnumerable<Comment>> Get(Guid gameId, CommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null) =>
        CommentQueries.Page(_dbContext, gameId, query, paging, excludeUserIds, "DM.GameComments.List");

    /// <inheritdoc />
    public Task<Comment?> Get(Guid commentId) =>
        CommentQueries.Single(_dbContext, commentId, CommentQueries.GameIds(_dbContext), "DM.GameComments.Get");

    /// <summary>
    /// The game's half of the last-comment move.
    /// </summary>
    /// <remarks>
    /// What the statement says and why it says it that way is stated once, on
    /// <see cref="CommentWrites.Create{TOwner}" />. Only the table is spelled out
    /// here: EF refuses raw SQL whose text is not a compile-time constant, so the
    /// three owners cannot share one statement with the table interpolated into it.
    /// </remarks>
    private const string MoveLastCommentForwardSql =
        """
        UPDATE "Games" SET "LastCommentId" = {1}
        WHERE "GameId" = {0}
          AND NOT EXISTS (
              SELECT 1 FROM "Comments" c
              WHERE c."CommentId" = "Games"."LastCommentId"
                AND (c."CreatedUtc", c."CommentId") > ({2}, {1}))
        """;

    /// <inheritdoc />
    public Task<Comment> Create(CreateGameCommentEntity createComment) =>
        CommentWrites.Create(
            _dbContext,
            _dbContext.Games,
            g => g.GameId == createComment.GameId,
            g => g.CommentCount,
            createComment.GameId,
            new CommentDal
            {
                CommentId = createComment.CommentId,
                EntityId = createComment.GameId,
                AuthorId = createComment.AuthorId,
                Text = createComment.Text,
                CreatedUtc = createComment.CreatedUtc,
                IsRemoved = false
            },
            MoveLastCommentForwardSql,
            "DM.GameComments.Created");

    /// <inheritdoc />
    public Task<Comment> Update(UpdateGameCommentEntity updateComment)
        => CommentWrites.Update(_dbContext, _guidFactory, updateComment.CommentId, updateComment.Text,
            updateComment.EditorUserId, updateComment.ModifiedUtc, "DM.GameComments.Updated");

    /// <inheritdoc />
    public Task<GameCommentToDelete?> GetForDelete(Guid commentId) =>
        CommentQueries.One(_dbContext, commentId, CommentQueries.GameIds(_dbContext))
        .TagWith("DM.GameComments.GetForDelete")
        .ProjectToGameCommentToDelete()
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<bool> Delete(DeleteGameCommentEntity deleteComment) =>
        CommentWrites.Delete(
            _dbContext,
            _dbContext.Games,
            g => g.GameId == deleteComment.GameId,
            g => g.GameId == deleteComment.GameId && g.LastCommentId == deleteComment.CommentId,
            g => g.CommentCount,
            g => g.LastCommentId,
            deleteComment.GameId,
            deleteComment.CommentId,
            deleteComment.DeletedByUserId,
            deleteComment.DeletedUtc,
            "DM.GameComments.NewestCommentIdExcept");
}
