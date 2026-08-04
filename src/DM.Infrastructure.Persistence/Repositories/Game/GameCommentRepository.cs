using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using CommentDal = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class GameCommentRepository : IGameCommentRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public GameCommentRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid gameId, GameCommentsQuery query, IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var dbQuery = _dbContext.Comments
            .TagWith("DM.GameComments.Count")
            .Where(c => !c.IsRemoved && c.EntityId == gameId);

        dbQuery = ApplyFilters(dbQuery, query, excludeUserIds);

        return dbQuery.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Comment>> Get(Guid gameId, GameCommentsQuery query, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var dbQuery = _dbContext.Comments
            .TagWith("DM.GameComments.List")
            .Where(c => !c.IsRemoved && c.EntityId == gameId);

        dbQuery = ApplyFilters(dbQuery, query, excludeUserIds);

        var orderedQuery = ApplySorting(dbQuery, query, _dbContext);

        return await orderedQuery
            .Page(paging)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    private static IQueryable<CommentDal> ApplyFilters(
        IQueryable<CommentDal> query,
        GameCommentsQuery commentsQuery,
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

    private static IOrderedQueryable<CommentDal> ApplySorting(
        IQueryable<CommentDal> query,
        GameCommentsQuery commentsQuery,
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
                    l.EntityType == LikeEntityType.Comment))
                : query.OrderBy(c => dbContext.Likes.Count(l =>
                    !l.IsRemoved &&
                    l.EntityId == c.CommentId &&
                    l.EntityType == LikeEntityType.Comment)),
            _ => isDescending // "created" or default
                ? query.OrderByDescending(c => c.CreatedUtc)
                : query.OrderBy(c => c.CreatedUtc)
        };
    }

    /// <inheritdoc />
    public Task<Comment?> Get(Guid commentId)
    {
        return _dbContext.Comments
            .TagWith("DM.GameComments.Get")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<Comment> Create(CreateGameCommentEntity createComment)
    {
        var comment = new CommentDal
        {
            CommentId = createComment.CommentId,
            EntityId = createComment.GameId,
            AuthorId = createComment.AuthorId,
            Text = createComment.Text,
            CreatedUtc = createComment.CreatedUtc,
            IsRemoved = false
        };

        _dbContext.Comments.Add(comment);

        // Update game comment count and last comment
        var game = await _dbContext.Games.FindAsync(createComment.GameId);
        if (game != null)
        {
            game.CommentCount = createComment.NewCommentCount;
            game.LastCommentId = createComment.CommentId;
        }

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Comments
            .TagWith("DM.GameComments.Created")
            .Where(c => c.CommentId == createComment.CommentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<Comment> Update(UpdateGameCommentEntity updateComment)
    {
        var comment = await _dbContext.Comments.FindAsync(updateComment.CommentId);
        if (comment != null)
        {
            comment.Text = updateComment.Text;
            // Modification tracking is handled via Edit history, not inline ModifiedUtc
            await _dbContext.SaveChangesAsync();
        }

        return await _dbContext.Comments
            .TagWith("DM.GameComments.Updated")
            .Where(c => c.CommentId == updateComment.CommentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<GameCommentToDelete?> GetForDelete(Guid commentId)
    {
        var comment = await _dbContext.Comments
            .TagWith("DM.GameComments.GetForDelete")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<GameCommentToDelete>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (comment == null) return null;

        var gameInfo = await _dbContext.Games
            .Where(g => g.GameId == comment.GameId)
            .Select(g => new { g.CommentCount, g.LastCommentId })
            .FirstOrDefaultAsync();

        if (gameInfo != null)
        {
            comment.GameCommentCount = gameInfo.CommentCount;
            comment.IsLastComment = gameInfo.LastCommentId == commentId;
        }

        return comment;
    }

    /// <inheritdoc />
    public async Task<Guid?> GetSecondLastCommentId(Guid gameId)
    {
        return await _dbContext.Comments
            .TagWith("DM.GameComments.SecondLastCommentId")
            .Where(c => !c.IsRemoved && c.EntityId == gameId)
            .OrderByDescending(c => c.CreatedUtc)
            .Skip(1)
            .Select(c => (Guid?)c.CommentId)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task Delete(DeleteGameCommentEntity deleteComment)
    {
        var comment = await _dbContext.Comments.FindAsync(deleteComment.CommentId);
        if (comment != null)
        {
            SoftDelete.Mark(comment, deleteComment.DeletedByUserId, deleteComment.DeletedUtc);
        }

        var game = await _dbContext.Games.FindAsync(deleteComment.GameId);
        if (game != null)
        {
            game.CommentCount = deleteComment.NewCommentCount;
            if (deleteComment.NewLastCommentId.HasValue || game.LastCommentId == deleteComment.CommentId)
            {
                game.LastCommentId = deleteComment.NewLastCommentId;
            }
        }

        await _dbContext.SaveChangesAsync();
    }
}
