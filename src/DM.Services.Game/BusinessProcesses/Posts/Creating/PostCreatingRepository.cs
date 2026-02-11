using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Output;
using Microsoft.EntityFrameworkCore;
using PostPendency = DM.Services.DataAccess.BusinessObjects.Games.Links.PostPendency;
using DbPost = DM.Services.DataAccess.BusinessObjects.Games.Posts.Post;

namespace DM.Services.Game.BusinessProcesses.Posts.Creating;

/// <inheritdoc />
internal class PostCreatingRepository : IPostCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PostCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Post> Create(DbPost post, IEnumerable<IUpdateBuilder<PostPendency>> postPendencyUpdates)
    {
        _dbContext.Posts.Add(post);
        foreach (var postPendencyUpdate in postPendencyUpdates)
        {
            postPendencyUpdate.AttachTo(_dbContext);
        }

        // Increment author's post count (QuantityRating)
        await _dbContext.Users
            .Where(u => u.UserId == post.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.QuantityRating, x => x.QuantityRating + 1));

        await _dbContext.SaveChangesAsync();
        return await _dbContext.Posts
            .Where(p => p.PostId == post.PostId)
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}