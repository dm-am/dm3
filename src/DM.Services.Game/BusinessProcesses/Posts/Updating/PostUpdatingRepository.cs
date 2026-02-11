using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Output;
using Microsoft.EntityFrameworkCore;
using DbPost = DM.Services.DataAccess.BusinessObjects.Games.Posts.Post;

namespace DM.Services.Game.BusinessProcesses.Posts.Updating;

/// <inheritdoc />
internal class PostUpdatingRepository : IPostUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PostUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Post?> Update(IUpdateBuilder<DbPost> updatePost)
    {
        var postId = updatePost.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.Posts
            .Where(p => p.PostId == postId)
            .ProjectTo<Post>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }
}