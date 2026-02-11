using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using DM.Services.Game.BusinessProcesses.Shared;
using DM.Services.Game.Dto.Output;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Game.BusinessProcesses.Posts.Reading;

/// <inheritdoc />
internal class PostReadingRepository : IPostReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PostReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid roomId, Guid userId)
    {
        return _dbContext.Rooms
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .Where(r => r.RoomId == roomId)
            .SelectMany(r => r.Posts)
            .CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Post>> Get(Guid roomId, PagingData paging, Guid userId)
    {
        return await _dbContext.Rooms
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .Where(r => r.RoomId == roomId)
            .SelectMany(r => r.Posts)
            .OrderBy(p => p.CreatedUtc)
            .Page(paging)
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public Task<Post?> Get(Guid postId, Guid userId)
    {
        return _dbContext.Rooms
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .SelectMany(r => r.Posts)
            .Where(p => p.PostId == postId)
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }

    /// <inheritdoc />
    public async Task<BestPostResult?> GetBestPost(Guid userId)
    {
        var result = await _dbContext.Posts
            .Where(p => p.UserId == userId)
            .Where(p => p.Room.AccessType == Core.Dto.Enums.RoomAccessType.Open)
            .Select(p => new
            {
                Post = p,
                Rating = _dbContext.Reviews
                    .Where(r => r.TargetId == p.PostId && r.TargetType == Core.Dto.Enums.ReviewTargetType.Post)
                    .Sum(r => (int?)r.SignValue) ?? 0
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
                AuthorLogin = x.Post.Author.Login,
                Rating = x.Rating,
                CreatedUtc = x.Post.CreatedUtc
            })
            .FirstOrDefaultAsync();

        return result;
    }
}