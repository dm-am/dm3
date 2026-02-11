using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using Comment = DM.Services.DataAccess.BusinessObjects.Common.Comment;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Updating;

/// <inheritdoc />
internal class CommentUpdatingRepository : ICommentUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CommentUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Services.Common.Dto.Comment> Update(IUpdateBuilder<Comment> update)
    {
        var commentId = update.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.Comments
            .TagWith("DM.Blogs.UpdatedComment")
            .Where(c => c.CommentId == commentId)
            .ProjectTo<Services.Common.Dto.Comment>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}
