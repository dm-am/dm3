using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Games;
using Microsoft.EntityFrameworkCore;
using CommentDal = DM.Infrastructure.Persistence.Entities.CrossDomain.Comment;

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
    public Task<int> Count(Guid gameId, IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var query = _dbContext.Comments
            .TagWith("DM.GameComments.Count")
            .Where(c => !c.IsRemoved && c.EntityId == gameId);

        if (excludeUserIds is { Count: > 0 })
        {
            query = query.Where(c => !excludeUserIds.Contains(c.AuthorId));
        }

        return query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Comment>> Get(Guid gameId, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var query = _dbContext.Comments
            .TagWith("DM.GameComments.List")
            .Where(c => !c.IsRemoved && c.EntityId == gameId);

        if (excludeUserIds is { Count: > 0 })
        {
            query = query.Where(c => !excludeUserIds.Contains(c.AuthorId));
        }

        return await query
            .OrderBy(c => c.CreatedUtc)
            .Page(paging)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
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
            comment.ModifiedUtc = updateComment.ModifiedUtc;
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
            comment.IsRemoved = true;
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
