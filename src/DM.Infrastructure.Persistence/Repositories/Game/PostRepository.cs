using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class PostRepository : IPostRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PostRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    #region Read Operations

    public Task<int> Count(Guid roomId, Guid userId)
    {
        return _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => r.RoomId == roomId)
            .SelectMany(r => r.Posts)
            .CountAsync();
    }

    public async Task<IEnumerable<Post>> Get(Guid roomId, PagingData paging, Guid userId)
    {
        return await _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => r.RoomId == roomId)
            .SelectMany(r => r.Posts)
            .OrderBy(p => p.CreatedUtc)
            .Page(paging)
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    public Task<Post?> Get(Guid postId, Guid userId)
    {
        return _dbContext.Posts
            .Where(p => p.PostId == postId)
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }

    public async Task<BestPostResult?> GetBestPost(Guid userId)
    {
        var result = await _dbContext.Posts
            .Where(p => p.AuthorId == userId)
            .Where(p => p.Room.AccessType == RoomAccessType.Open)
            .Select(p => new
            {
                Post = p,
                Reviews = _dbContext.Reviews
                    .Where(r => r.TargetId == p.PostId && r.TargetType == ReviewTargetType.Post)
            })
            .Select(x => new
            {
                x.Post,
                Rating = x.Reviews.Sum(r => (int?)r.SignValue) ?? 0,
                ReviewCount = x.Reviews.Count()
            })
            .Where(x => x.Rating > 0)
            .OrderByDescending(x => x.Rating)
            .Select(x => new BestPostResult
            {
                PostId = x.Post.PostId,
                Text = x.Post.Text,
                GameTitle = x.Post.Room.Game.Title,
                GameId = x.Post.Room.Game.GameId,
                RoomTitle = x.Post.Room.Title,
                RoomId = x.Post.RoomId,
                AuthorUsername = x.Post.Author.Username,
                Rating = x.Rating,
                ReviewCount = x.ReviewCount,
                CreatedUtc = x.Post.CreatedUtc
            })
            .FirstOrDefaultAsync();
        return result;
    }

    #endregion

    #region Write Operations

    public async Task<Post> Create(CreatePostEntity createPost)
    {
        var dbPost = new DbPost
        {
            PostId = createPost.PostId,
            RoomId = createPost.RoomId,
            CharacterId = createPost.CharacterId,
            AuthorId = createPost.AuthorId,
            CreatedUtc = createPost.CreatedUtc,
            Text = createPost.Text,
            Comment = createPost.Comment,
            MasterMessage = createPost.MasterMessage,
            IsRemoved = false
        };
        _dbContext.Posts.Add(dbPost);

        // Increment author's post count (QuantityRating)
        await _dbContext.Users
            .Where(u => u.UserId == createPost.AuthorId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.QuantityRating, x => x.QuantityRating + 1));

        await _dbContext.SaveChangesAsync();
        return await _dbContext.Posts
            .Where(p => p.PostId == createPost.PostId)
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    public async Task<Post?> Update(UpdatePostEntity updatePost)
    {
        var post = await _dbContext.Posts.FindAsync(updatePost.PostId);
        if (post == null)
            return null;

        // Update text always
        post.Text = updatePost.Text;
        post.Comment = updatePost.Comment;
        post.MasterMessage = updatePost.MasterMessage;

        // Update character if requested
        if (updatePost.ShouldChangeCharacter)
            post.CharacterId = updatePost.CharacterId;

        post.ModifiedUtc = updatePost.ModifiedUtc;

        // Handle soft delete if requested
        if (updatePost.IsRemoved.HasValue)
            post.IsRemoved = updatePost.IsRemoved.Value;

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Posts
            .Where(p => p.PostId == updatePost.PostId)
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task Delete(Guid postId)
    {
        var post = await _dbContext.Posts.FindAsync(postId);
        if (post != null)
        {
            post.IsRemoved = true;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task DecrementAuthorQuantityRating(Guid authorId)
    {
        await _dbContext.Users
            .Where(u => u.UserId == authorId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.QuantityRating, x => x.QuantityRating - 1));
    }

    #endregion
}
